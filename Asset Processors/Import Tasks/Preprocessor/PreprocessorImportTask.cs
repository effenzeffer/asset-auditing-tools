using System;
using System.Collections.Generic;
using AssetTools.GUIUtility;
using UnityEditor;
using UnityEngine;

namespace AssetTools
{
	[Serializable]
	public class PreprocessorImportTask : BaseImportTask
	{
		/// <summary>
		/// TODO If any of this is changed, do the Assets imported by it need to be reimported?
		/// </summary>
		
		private const string kImportTaskName = "PreprocessorImportTask";
		
		[SerializeField] private string m_MethodString = "";
		[SerializeField] private string m_Data = "";

		private PreprocessorImportTaskInspector m_Inspector = null;
		internal ProcessorMethodInfo m_ProcessorMethodInfo;
		
		public string methodString
		{
			get { return m_MethodString; }
		}
		
		public override Type GetConformObjectType()
		{
			return typeof(PreprocessorConformObject);
		}

		public override string AssetMenuFixString
		{
			get
			{
				return Method == null ? "None Selected" : "Import using " + Method.TypeName;
			}
		}
		
		public override bool CanProcess( AssetImporter item )
		{
			return true;
		}

		public override List<IConformObject> GetConformObjects( string asset, ImportDefinitionProfile profile )
		{
			// Preprocessor versionCode comparison
			// will need someway to store this. It could not work well if imported not using it
			// 1: add it to meta data. Only option is userData, which could conflict with other code packages. This would make it included in the hash for cache server. Which would be required.
			// 2: store a database of imported version data. Could be tricky to keep in sync
			// 3: AssetDatabaseV2 supports asset dependencies
			
			List<IConformObject> infos = new List<IConformObject>();

			if( Method == null )
			{
				PreprocessorConformObject conformObject = new PreprocessorConformObject( "None Selected", 0 );
				infos.Add( conformObject );
				return infos;
			}

			UserDataSerialization userData = UserDataSerialization.Get( asset );
			List<UserDataSerialization.PostprocessorData> data = userData.m_ImporterPostprocessorData.assetProcessedWith;
			string profileGuid = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath( profile ) );

			if( data != null )
			{
				for( int i = 0; i < data.Count; ++i )
				{
					if( data[i].moduleName != kImportTaskName ||
					    data[i].typeName != Method.TypeName ||
					    data[i].assemblyName != Method.AssemblyName ||
					    data[i].importDefinitionGUID != profileGuid )
						continue;

					PreprocessorConformObject conformObject = new PreprocessorConformObject( data[i].typeName, data[i].version, Method.Version );
					infos.Add( conformObject );
					break;
				}
			}
			else
			{
				PreprocessorConformObject conformObject = new PreprocessorConformObject( Method.TypeName, Method.Version );
				infos.Add( conformObject );
			}
			return infos;
		}
		
		private void SetUserData( AssetImporter importer, ImportDefinitionProfile profile )
		{
			UserDataSerialization data = UserDataSerialization.Get( importer.assetPath );
			string profileGuid = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath( profile ) );
			data.m_ImporterPostprocessorData.UpdateOrAdd( new UserDataSerialization.PostprocessorData( profileGuid, kImportTaskName, Method.AssemblyName, Method.TypeName, Method.Version ) );
			data.UpdateImporterUserData();
		}
		
		public override bool Apply( AssetImporter item, ImportDefinitionProfile fromProfile )
		{
			if( string.IsNullOrEmpty( m_MethodString ) == false )
			{
				if( Method != null )
				{
					object returnValue = Method.Invoke( item, m_Data );
					if( returnValue != null )
					{
						SetUserData( item, fromProfile );
						return (bool) returnValue;
					}
				}
			}
			return false;
		}
		
		private ProcessorMethodInfo Method
		{
			get
			{
				if( m_ProcessorMethodInfo == null && string.IsNullOrEmpty( m_MethodString ) == false )
				{
					string assemblyName;
					string typeString;
					GetMethodStrings( out assemblyName, out typeString );
					if( string.IsNullOrEmpty( typeString ) )
					{
						Debug.LogError( "Error collecting method from " + m_MethodString );
						return null;
					}
					
					List<ProcessorMethodInfo> methods = PreprocessorImplementorCache.Methods;
					for( int i = 0; i < methods.Count; ++i )
					{
						if( assemblyName != null && methods[i].AssemblyName.StartsWith( assemblyName ) == false )
							continue;

						if( methods[i].TypeName.EndsWith( typeString ) )
						{
							m_ProcessorMethodInfo = methods[i];
							break;
						}
					}
				}
		
				return m_ProcessorMethodInfo;
			}
		}

		private void GetMethodStrings( out string assemblyName, out string typeString )
		{
			int commaIndex = m_MethodString.IndexOf( ',' );
			if( commaIndex > 0 )
			{
				assemblyName = m_MethodString.Substring( commaIndex + 2 );
				typeString = m_MethodString.Substring( 0, commaIndex );
			}
			else
			{
				assemblyName = "";
				typeString = m_MethodString;
			}
		}
		
		public override void DrawGUI( ControlRect layout )
		{
			if( m_Inspector == null )
				m_Inspector = new PreprocessorImportTaskInspector();
			
			m_Inspector.Draw( SelfSerializedObject, layout );
			SelfSerializedObject.ApplyModifiedProperties();
		}
		
		public override int GetHashCode()
		{
			return (m_MethodString.GetHashCode() * 31) + (m_Data.GetHashCode() * 31) + (kImportTaskName.GetHashCode() * 31);
		}
	}
}