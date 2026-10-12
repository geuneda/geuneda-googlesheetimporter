using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.GoogleSheetImporter
{
	/// <summary>
	/// <seealso cref="CsvParser"/>에서 필드의 파싱을 무시하기 위한 어트리뷰트
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class ParseIgnoreAttribute : Attribute
	{
	}
}