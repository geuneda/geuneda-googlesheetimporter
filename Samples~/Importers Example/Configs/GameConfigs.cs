using System;
using Geuneda.DataExtensions;
using UnityEngine;

namespace Configs
{
	/// <summary>
	/// <c>GameConfigsImporter</c> 시트 전체(Key/Value 행)에 해당하는 예제 설정.
	/// 필드 이름은 시트의 Key 값과 같아야 하며, 시트에 맞게 필드를 추가합니다
	/// </summary>
	[Serializable]
	public struct GameConfig
	{
		public int MaxLevel;
		public float Speed;
	}

	/// <summary>
	/// <see cref="GameConfig"/> 하나를 저장하는 예제 ScriptableObject
	/// </summary>
	public class GameConfigs : ScriptableObject, ISingleConfigContainer<GameConfig>
	{
		[SerializeField] private GameConfig _config;

		/// <inheritdoc />
		public GameConfig Config
		{
			get => _config;
			set => _config = value;
		}
	}
}
