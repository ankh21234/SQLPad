﻿using System.Collections;
using System.Collections.Generic;

namespace SqlPad
{
	public interface ILargeValue
	{
		string DataTypeName { get; }

		bool IsEditable { get; }

		long Length { get; }

		void Prefetch();
	}

	public interface ILargeTextValue: ILargeValue
	{
		string Preview { get; }

		string Value { get; }

		string GetChunk(int offset, int length);
	}

	public interface ILargeBinaryValue : ILargeValue
	{
		byte[] Value { get; }

		byte[] GetChunk(int bytes);
	}

	public interface ICollectionValue : ILargeValue
	{
		IList Records { get; }

		ColumnHeader ColumnHeader { get; }
	}

	public interface IComplexType : ILargeValue
	{
		ICollection<CustomTypeAttributeValue> Attributes { get; }
	}

	public struct CustomTypeAttributeValue
	{
		public ColumnHeader ColumnHeader { get; set; }
		
		public object Value { get; set; }
	}
}
