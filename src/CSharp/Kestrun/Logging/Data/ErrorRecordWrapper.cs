﻿using System.Collections.ObjectModel;
using System.Management.Automation;
using Kestrun.Logging.Enrichers.Extensions;

namespace Kestrun.Logging.Data;

public class ErrorRecordWrapper
{
	public ErrorCategoryInfo CategoryInfo { get; }
	public ErrorDetails ErrorDetails { get; set; }
	public string FullyQualifiedErrorId { get; }
	public InvocationInfoWrapper InvocationInfoWrapper { get; }
	public ReadOnlyCollection<int> PipelineIterationInfo { get; }
	public string ScriptStackTrace { get; }
	public object TargetObject { get; }
	public string? ExceptionMessage { get; }
	public string? ExceptionDetails { get; }

	public ErrorRecordWrapper(ErrorRecord errorRecord)
	{
		CategoryInfo = errorRecord.CategoryInfo;
		ErrorDetails = errorRecord.ErrorDetails;
		FullyQualifiedErrorId = errorRecord.FullyQualifiedErrorId;
		InvocationInfoWrapper = new InvocationInfoWrapper(errorRecord.InvocationInfo);
		PipelineIterationInfo = errorRecord.PipelineIterationInfo;
		ScriptStackTrace = errorRecord.ScriptStackTrace;
		TargetObject = errorRecord.TargetObject;
		ExceptionMessage = errorRecord.Exception?.Message;
		ExceptionDetails = errorRecord.Exception?.ToString();
	}

	public override string ToString()
	{
		return this.ToTable();
	}
}
