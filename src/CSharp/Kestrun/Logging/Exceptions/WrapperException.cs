﻿using System;
using System.Management.Automation;
using Kestrun.Logging.Data;

namespace Kestrun.Logging.Exceptions;

public class WrapperException : Exception
{
	public ErrorRecordWrapper? ErrorRecordWrapper { get; }

	public WrapperException(string message) : base(message)
	{
	}

	public WrapperException(string message, Exception innerException) : base(message, innerException)
	{
	}

	public WrapperException(Exception innerException, ErrorRecord errorRecord) : base(string.Empty, innerException)
	{
		ErrorRecordWrapper = new ErrorRecordWrapper(errorRecord);
	}

	public WrapperException()
	{
	}

	public override string ToString()
	{
		return InnerException?.ToString() ?? string.Empty;
	}
}
