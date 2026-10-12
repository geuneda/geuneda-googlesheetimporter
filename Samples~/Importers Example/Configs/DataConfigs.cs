using System;
using System.Collections.Generic;
using Geuneda.DataExtensions;
using UnityEngine;

namespace Configs
{
	/// <summary>
	/// <c>DataConfigsImporter</c> 시트의 1행에 해당하는 예제 설정.
	/// 필드 이름은 시트 헤더와 같아야 하며, 시트에 맞게 필드를 추가합니다
	/// </summary>
	[Serializable]
	public struct DataConfig
	{
		public int Id;
		public string Name;
	}

	/// <summary>
	/// <see cref="DataConfig"/> 목록을 저장하는 예제 ScriptableObject
	/// </summary>
	public class DataConfigs : ScriptableObject, IConfigsContainer<DataConfig>
	{
		[SerializeField] private List<DataConfig> _configs = new List<DataConfig>();

		/// <inheritdoc />
		public List<DataConfig> Configs
		{
			get => _configs;
			set => _configs = value;
		}
	}
}
