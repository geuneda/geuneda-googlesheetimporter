using System;

// ReSharper disable once CheckNamespace

namespace GeunedaEditor.GoogleSheetImporter
{
	/// <summary>
	/// <seealso cref="GoogleSheetImporter"/>의 데이터 임포트 순서를 설정하는 어트리뷰트
	/// 숫자가 작을수록 먼저 임포트됩니다. 기본값 = int.MaxValue
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class GoogleSheetImportOrderAttribute : Attribute
	{
		/// <summary>
		/// 시트의 임포트 순서. 값이 작을수록 먼저 임포트됩니다
		/// </summary>
		public int ImportOrder { get; }
		
		public GoogleSheetImportOrderAttribute(int importOrder)
		{
			ImportOrder = importOrder;
		}
	}
}