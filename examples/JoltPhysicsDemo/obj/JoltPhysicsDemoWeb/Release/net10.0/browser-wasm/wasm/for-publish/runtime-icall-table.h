[
{ "klass": "", "icalls": []},
 { "klass":"Mono.RuntimeClassHandle", "icalls": [{} 	,{ "name": "GetTypeFromClass", "func": "ves_icall_Mono_RuntimeClassHandle_GetTypeFromClass", "handles": false, "flags": "0" }
]},
 { "klass":"Mono.RuntimeGPtrArrayHandle", "icalls": [{} 	,{ "name": "GPtrArrayFree", "func": "ves_icall_Mono_RuntimeGPtrArrayHandle_GPtrArrayFree", "handles": false, "flags": "0" }
]},
 { "klass":"Mono.SafeStringMarshal", "icalls": [{} 	,{ "name": "GFree", "func": "ves_icall_Mono_SafeStringMarshal_GFree", "handles": false, "flags": "0" }
	,{ "name": "StringToUtf8_icall", "func": "ves_icall_Mono_SafeStringMarshal_StringToUtf8", "handles": false, "flags": "0" }
]},
 { "klass":"System.ArgIterator", "icalls": [{} 	,{ "name": "IntGetNextArg", "func": "ves_icall_System_ArgIterator_IntGetNextArg", "handles": false, "flags": "0" }
	,{ "name": "IntGetNextArgType", "func": "ves_icall_System_ArgIterator_IntGetNextArgType", "handles": false, "flags": "0" }
	,{ "name": "IntGetNextArgWithType", "func": "ves_icall_System_ArgIterator_IntGetNextArgWithType", "handles": false, "flags": "0" }
	,{ "name": "Setup", "func": "ves_icall_System_ArgIterator_Setup", "handles": false, "flags": "0" }
]},
 { "klass":"System.Array", "icalls": [{} 	,{ "name": "CanChangePrimitive", "func": "ves_icall_System_Array_CanChangePrimitive", "handles": false, "flags": "0" }
	,{ "name": "FastCopy", "func": "ves_icall_System_Array_FastCopy", "handles": false, "flags": "0" }
	,{ "name": "GetCorElementTypeOfElementTypeInternal", "func": "ves_icall_System_Array_GetCorElementTypeOfElementTypeInternal", "handles": false, "flags": "0" }
	,{ "name": "GetGenericValue_icall", "func": "ves_icall_System_Array_GetGenericValue_icall", "handles": false, "flags": "0" }
	,{ "name": "GetLengthInternal", "func": "ves_icall_System_Array_GetLengthInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetLowerBoundInternal", "func": "ves_icall_System_Array_GetLowerBoundInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetValueImpl", "func": "ves_icall_System_Array_GetValueImpl_raw", "handles": true, "flags": "4" }
	,{ "name": "InitializeInternal", "func": "ves_icall_System_Array_InitializeInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalCreate", "func": "ves_icall_System_Array_InternalCreate", "handles": false, "flags": "0" }
	,{ "name": "IsValueOfElementTypeInternal", "func": "ves_icall_System_Array_IsValueOfElementTypeInternal", "handles": false, "flags": "0" }
	,{ "name": "SetGenericValue_icall", "func": "ves_icall_System_Array_SetGenericValue_icall", "handles": false, "flags": "0" }
	,{ "name": "SetValueImpl", "func": "ves_icall_System_Array_SetValueImpl_raw", "handles": true, "flags": "4" }
	,{ "name": "SetValueRelaxedImpl", "func": "ves_icall_System_Array_SetValueRelaxedImpl_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Buffer", "icalls": [{} 	,{ "name": "BulkMoveWithWriteBarrier", "func": "ves_icall_System_Buffer_BulkMoveWithWriteBarrier", "handles": false, "flags": "0" }
	,{ "name": "MemmoveInternal", "func": "ves_icall_System_Runtime_RuntimeImports_Memmove", "handles": false, "flags": "0" }
	,{ "name": "ZeroMemoryInternal", "func": "ves_icall_System_Runtime_RuntimeImports_ZeroMemory", "handles": false, "flags": "0" }
]},
 { "klass":"System.Delegate", "icalls": [{} 	,{ "name": "AllocDelegateLike_internal", "func": "ves_icall_System_Delegate_AllocDelegateLike_internal_raw", "handles": true, "flags": "4" }
	,{ "name": "CreateDelegate_internal", "func": "ves_icall_System_Delegate_CreateDelegate_internal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetVirtualMethod_internal", "func": "ves_icall_System_Delegate_GetVirtualMethod_internal_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Diagnostics.Debugger", "icalls": [{} 	,{ "name": "IsAttached_internal", "func": "ves_icall_System_Diagnostics_Debugger_IsAttached_internal", "handles": false, "flags": "0" }
	,{ "name": "IsLogging", "func": "ves_icall_System_Diagnostics_Debugger_IsLogging", "handles": false, "flags": "0" }
	,{ "name": "Log_icall", "func": "ves_icall_System_Diagnostics_Debugger_Log", "handles": false, "flags": "0" }
]},
 { "klass":"System.Diagnostics.StackFrame", "icalls": [{} 	,{ "name": "GetFrameInfo", "func": "ves_icall_System_Diagnostics_StackFrame_GetFrameInfo", "handles": false, "flags": "0" }
]},
 { "klass":"System.Diagnostics.StackTrace", "icalls": [{} 	,{ "name": "GetTrace", "func": "ves_icall_System_Diagnostics_StackTrace_GetTrace", "handles": false, "flags": "0" }
]},
 { "klass":"System.Diagnostics.Tracing.EventPipeInternal", "icalls": [{} 	,{ "name": "CreateProvider", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_CreateProvider_raw", "handles": true, "flags": "4" }
	,{ "name": "DefineEvent", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_DefineEvent", "handles": false, "flags": "0" }
	,{ "name": "DeleteProvider", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_DeleteProvider", "handles": false, "flags": "0" }
	,{ "name": "Disable", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_Disable", "handles": false, "flags": "0" }
	,{ "name": "Enable", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_Enable", "handles": false, "flags": "0" }
	,{ "name": "EventActivityIdControl", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_EventActivityIdControl", "handles": false, "flags": "0" }
	,{ "name": "GetNextEvent", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_GetNextEvent", "handles": false, "flags": "0" }
	,{ "name": "GetProvider", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_GetProvider", "handles": false, "flags": "0" }
	,{ "name": "GetRuntimeCounterValue", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_GetRuntimeCounterValue", "handles": false, "flags": "0" }
	,{ "name": "GetSessionInfo", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_GetSessionInfo", "handles": false, "flags": "0" }
	,{ "name": "SignalSession", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_SignalSession", "handles": false, "flags": "0" }
	,{ "name": "WaitForSessionSignal", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_WaitForSessionSignal", "handles": false, "flags": "0" }
	,{ "name": "WriteEventData", "func": "ves_icall_System_Diagnostics_Tracing_EventPipeInternal_WriteEventData", "handles": false, "flags": "0" }
]},
 { "klass":"System.Diagnostics.Tracing.NativeRuntimeEventSource", "icalls": [{} 	,{ "name": "LogContentionLockCreated", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogContentionLockCreated", "handles": false, "flags": "0" }
	,{ "name": "LogContentionStart", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogContentionStart", "handles": false, "flags": "0" }
	,{ "name": "LogContentionStop", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogContentionStop", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolIODequeue", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolIODequeue", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolIOEnqueue", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolIOEnqueue", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolIOPack", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolIOPack", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolMinMaxThreads", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolMinMaxThreads", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkerThreadAdjustmentAdjustment", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkerThreadAdjustmentAdjustment", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkerThreadAdjustmentSample", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkerThreadAdjustmentSample", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkerThreadAdjustmentStats", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkerThreadAdjustmentStats", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkerThreadStart", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkerThreadStart", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkerThreadStop", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkerThreadStop", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkerThreadWait", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkerThreadWait", "handles": false, "flags": "0" }
	,{ "name": "LogThreadPoolWorkingThreadCount", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogThreadPoolWorkingThreadCount", "handles": false, "flags": "0" }
	,{ "name": "LogWaitHandleWaitStart", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogWaitHandleWaitStart", "handles": false, "flags": "0" }
	,{ "name": "LogWaitHandleWaitStop", "func": "ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogWaitHandleWaitStop", "handles": false, "flags": "0" }
]},
 { "klass":"System.Enum", "icalls": [{} 	,{ "name": "GetEnumValuesAndNames", "func": "ves_icall_System_Enum_GetEnumValuesAndNames_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetCorElementType", "func": "ves_icall_System_Enum_InternalGetCorElementType", "handles": false, "flags": "0" }
	,{ "name": "InternalGetUnderlyingType", "func": "ves_icall_System_Enum_InternalGetUnderlyingType_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Environment", "icalls": [{} 	,{ "name": "Exit", "func": "ves_icall_System_Environment_Exit", "handles": false, "flags": "0" }
	,{ "name": "FailFast", "func": "ves_icall_System_Environment_FailFast_raw", "handles": true, "flags": "4" }
	,{ "name": "GetCommandLineArgs", "func": "ves_icall_System_Environment_GetCommandLineArgs_raw", "handles": true, "flags": "4" }
	,{ "name": "GetProcessorCount", "func": "ves_icall_System_Environment_get_ProcessorCount", "handles": false, "flags": "0" }
	,{ "name": "get_ExitCode", "func": "mono_environment_exitcode_get", "handles": false, "flags": "0" }
	,{ "name": "set_ExitCode", "func": "mono_environment_exitcode_set", "handles": false, "flags": "0" }
]},
 { "klass":"System.GC", "icalls": [{} 	,{ "name": "AddPressure", "func": "ves_icall_System_GC_AddPressure", "handles": false, "flags": "0" }
	,{ "name": "AllocPinnedArray", "func": "ves_icall_System_GC_AllocPinnedArray_raw", "handles": true, "flags": "4" }
	,{ "name": "GetAllocatedBytesForCurrentThread", "func": "ves_icall_System_GC_GetAllocatedBytesForCurrentThread", "handles": false, "flags": "0" }
	,{ "name": "GetCollectionCount", "func": "ves_icall_System_GC_GetCollectionCount", "handles": false, "flags": "0" }
	,{ "name": "GetGeneration", "func": "ves_icall_System_GC_GetGeneration_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMaxGeneration", "func": "ves_icall_System_GC_GetMaxGeneration", "handles": false, "flags": "0" }
	,{ "name": "GetTotalAllocatedBytes", "func": "ves_icall_System_GC_GetTotalAllocatedBytes_raw", "handles": true, "flags": "4" }
	,{ "name": "GetTotalMemory", "func": "ves_icall_System_GC_GetTotalMemory", "handles": false, "flags": "0" }
	,{ "name": "InternalCollect", "func": "ves_icall_System_GC_InternalCollect", "handles": false, "flags": "0" }
	,{ "name": "RemovePressure", "func": "ves_icall_System_GC_RemovePressure", "handles": false, "flags": "0" }
	,{ "name": "WaitForPendingFinalizers", "func": "ves_icall_System_GC_WaitForPendingFinalizers", "handles": false, "flags": "0" }
	,{ "name": "_GetGCMemoryInfo", "func": "ves_icall_System_GC_GetGCMemoryInfo", "handles": false, "flags": "0" }
	,{ "name": "_ReRegisterForFinalize", "func": "ves_icall_System_GC_ReRegisterForFinalize_raw", "handles": true, "flags": "4" }
	,{ "name": "_SuppressFinalize", "func": "ves_icall_System_GC_SuppressFinalize_raw", "handles": true, "flags": "4" }
	,{ "name": "get_ephemeron_tombstone", "func": "ves_icall_System_GC_get_ephemeron_tombstone_raw", "handles": true, "flags": "4" }
	,{ "name": "register_ephemeron_array", "func": "ves_icall_System_GC_register_ephemeron_array_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.IO.Stream", "icalls": [{} 	,{ "name": "HasOverriddenBeginEndRead", "func": "ves_icall_System_IO_Stream_HasOverriddenBeginEndRead_raw", "handles": true, "flags": "4" }
	,{ "name": "HasOverriddenBeginEndWrite", "func": "ves_icall_System_IO_Stream_HasOverriddenBeginEndWrite_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Math", "icalls": [{} 	,{ "name": "Acos", "func": "ves_icall_System_Math_Acos", "handles": false, "flags": "0" }
	,{ "name": "Acosh", "func": "ves_icall_System_Math_Acosh", "handles": false, "flags": "0" }
	,{ "name": "Asin", "func": "ves_icall_System_Math_Asin", "handles": false, "flags": "0" }
	,{ "name": "Asinh", "func": "ves_icall_System_Math_Asinh", "handles": false, "flags": "0" }
	,{ "name": "Atan", "func": "ves_icall_System_Math_Atan", "handles": false, "flags": "0" }
	,{ "name": "Atan2", "func": "ves_icall_System_Math_Atan2", "handles": false, "flags": "0" }
	,{ "name": "Atanh", "func": "ves_icall_System_Math_Atanh", "handles": false, "flags": "0" }
	,{ "name": "Cbrt", "func": "ves_icall_System_Math_Cbrt", "handles": false, "flags": "0" }
	,{ "name": "Ceiling", "func": "ves_icall_System_Math_Ceiling", "handles": false, "flags": "0" }
	,{ "name": "Cos", "func": "ves_icall_System_Math_Cos", "handles": false, "flags": "0" }
	,{ "name": "Cosh", "func": "ves_icall_System_Math_Cosh", "handles": false, "flags": "0" }
	,{ "name": "Exp", "func": "ves_icall_System_Math_Exp", "handles": false, "flags": "0" }
	,{ "name": "Floor", "func": "ves_icall_System_Math_Floor", "handles": false, "flags": "0" }
	,{ "name": "FusedMultiplyAdd", "func": "ves_icall_System_Math_FusedMultiplyAdd", "handles": false, "flags": "0" }
	,{ "name": "Log", "func": "ves_icall_System_Math_Log", "handles": false, "flags": "0" }
	,{ "name": "Log10", "func": "ves_icall_System_Math_Log10", "handles": false, "flags": "0" }
	,{ "name": "Log2", "func": "ves_icall_System_Math_Log2", "handles": false, "flags": "0" }
	,{ "name": "ModF", "func": "ves_icall_System_Math_ModF", "handles": false, "flags": "0" }
	,{ "name": "Pow", "func": "ves_icall_System_Math_Pow", "handles": false, "flags": "0" }
	,{ "name": "Round", "func": "ves_icall_System_Math_Round", "handles": false, "flags": "0" }
	,{ "name": "Sin", "func": "ves_icall_System_Math_Sin", "handles": false, "flags": "0" }
	,{ "name": "Sinh", "func": "ves_icall_System_Math_Sinh", "handles": false, "flags": "0" }
	,{ "name": "Sqrt", "func": "ves_icall_System_Math_Sqrt", "handles": false, "flags": "0" }
	,{ "name": "Tan", "func": "ves_icall_System_Math_Tan", "handles": false, "flags": "0" }
	,{ "name": "Tanh", "func": "ves_icall_System_Math_Tanh", "handles": false, "flags": "0" }
]},
 { "klass":"System.MathF", "icalls": [{} 	,{ "name": "Acos", "func": "ves_icall_System_MathF_Acos", "handles": false, "flags": "0" }
	,{ "name": "Acosh", "func": "ves_icall_System_MathF_Acosh", "handles": false, "flags": "0" }
	,{ "name": "Asin", "func": "ves_icall_System_MathF_Asin", "handles": false, "flags": "0" }
	,{ "name": "Asinh", "func": "ves_icall_System_MathF_Asinh", "handles": false, "flags": "0" }
	,{ "name": "Atan", "func": "ves_icall_System_MathF_Atan", "handles": false, "flags": "0" }
	,{ "name": "Atan2", "func": "ves_icall_System_MathF_Atan2", "handles": false, "flags": "0" }
	,{ "name": "Atanh", "func": "ves_icall_System_MathF_Atanh", "handles": false, "flags": "0" }
	,{ "name": "Cbrt", "func": "ves_icall_System_MathF_Cbrt", "handles": false, "flags": "0" }
	,{ "name": "Ceiling", "func": "ves_icall_System_MathF_Ceiling", "handles": false, "flags": "0" }
	,{ "name": "Cos", "func": "ves_icall_System_MathF_Cos", "handles": false, "flags": "0" }
	,{ "name": "Cosh", "func": "ves_icall_System_MathF_Cosh", "handles": false, "flags": "0" }
	,{ "name": "Exp", "func": "ves_icall_System_MathF_Exp", "handles": false, "flags": "0" }
	,{ "name": "Floor", "func": "ves_icall_System_MathF_Floor", "handles": false, "flags": "0" }
	,{ "name": "FusedMultiplyAdd", "func": "ves_icall_System_MathF_FusedMultiplyAdd", "handles": false, "flags": "0" }
	,{ "name": "Log", "func": "ves_icall_System_MathF_Log", "handles": false, "flags": "0" }
	,{ "name": "Log10", "func": "ves_icall_System_MathF_Log10", "handles": false, "flags": "0" }
	,{ "name": "Log2", "func": "ves_icall_System_MathF_Log2", "handles": false, "flags": "0" }
	,{ "name": "ModF(single,single*)", "func": "ves_icall_System_MathF_ModF", "handles": false, "flags": "0" }
	,{ "name": "Pow", "func": "ves_icall_System_MathF_Pow", "handles": false, "flags": "0" }
	,{ "name": "Sin", "func": "ves_icall_System_MathF_Sin", "handles": false, "flags": "0" }
	,{ "name": "Sinh", "func": "ves_icall_System_MathF_Sinh", "handles": false, "flags": "0" }
	,{ "name": "Sqrt", "func": "ves_icall_System_MathF_Sqrt", "handles": false, "flags": "0" }
	,{ "name": "Tan", "func": "ves_icall_System_MathF_Tan", "handles": false, "flags": "0" }
	,{ "name": "Tanh", "func": "ves_icall_System_MathF_Tanh", "handles": false, "flags": "0" }
]},
 { "klass":"System.Object", "icalls": [{} 	,{ "name": "MemberwiseClone", "func": "ves_icall_System_Object_MemberwiseClone_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Assembly", "icalls": [{} 	,{ "name": "GetCallingAssembly", "func": "ves_icall_System_Reflection_Assembly_GetCallingAssembly_raw", "handles": true, "flags": "4" }
	,{ "name": "GetEntryAssemblyNative", "func": "ves_icall_System_Reflection_Assembly_GetEntryAssembly_raw", "handles": true, "flags": "4" }
	,{ "name": "GetExecutingAssembly", "func": "ves_icall_System_Reflection_Assembly_GetExecutingAssembly_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetType", "func": "ves_icall_System_Reflection_Assembly_InternalGetType_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalLoad", "func": "ves_icall_System_Reflection_Assembly_InternalLoad_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.AssemblyName", "icalls": [{} 	,{ "name": "FreeAssemblyName", "func": "ves_icall_System_Reflection_AssemblyName_FreeAssemblyName", "handles": false, "flags": "0" }
	,{ "name": "GetNativeName", "func": "ves_icall_System_Reflection_AssemblyName_GetNativeName", "handles": false, "flags": "0" }
]},
 { "klass":"System.Reflection.CustomAttribute", "icalls": [{} 	,{ "name": "GetCustomAttributesDataInternal", "func": "ves_icall_MonoCustomAttrs_GetCustomAttributesDataInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetCustomAttributesInternal", "func": "ves_icall_MonoCustomAttrs_GetCustomAttributesInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "IsDefinedInternal", "func": "ves_icall_MonoCustomAttrs_IsDefinedInternal_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.CustomAttributeBuilder", "icalls": [{} 	,{ "name": "GetBlob", "func": "ves_icall_CustomAttributeBuilder_GetBlob_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.DynamicMethod", "icalls": [{} 	,{ "name": "create_dynamic_method", "func": "ves_icall_DynamicMethod_create_dynamic_method_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.RuntimeAssemblyBuilder", "icalls": [{} 	,{ "name": "UpdateNativeCustomAttributes", "func": "ves_icall_AssemblyBuilder_UpdateNativeCustomAttributes_raw", "handles": true, "flags": "4" }
	,{ "name": "basic_init", "func": "ves_icall_AssemblyBuilder_basic_init_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.RuntimeEnumBuilder", "icalls": [{} 	,{ "name": "setup_enum_type", "func": "ves_icall_EnumBuilder_setup_enum_type_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.RuntimeModuleBuilder", "icalls": [{} 	,{ "name": "GetRegisteredToken", "func": "ves_icall_ModuleBuilder_GetRegisteredToken_raw", "handles": true, "flags": "4" }
	,{ "name": "RegisterToken", "func": "ves_icall_ModuleBuilder_RegisterToken_raw", "handles": true, "flags": "4" }
	,{ "name": "basic_init", "func": "ves_icall_ModuleBuilder_basic_init_raw", "handles": true, "flags": "4" }
	,{ "name": "getMethodToken", "func": "ves_icall_ModuleBuilder_getMethodToken_raw", "handles": true, "flags": "4" }
	,{ "name": "getToken", "func": "ves_icall_ModuleBuilder_getToken_raw", "handles": true, "flags": "4" }
	,{ "name": "getUSIndex", "func": "ves_icall_ModuleBuilder_getUSIndex_raw", "handles": true, "flags": "4" }
	,{ "name": "set_wrappers_type", "func": "ves_icall_ModuleBuilder_set_wrappers_type_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.RuntimeTypeBuilder", "icalls": [{} 	,{ "name": "create_runtime_class", "func": "ves_icall_TypeBuilder_create_runtime_class_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.Emit.SignatureHelper", "icalls": [{} 	,{ "name": "get_signature_field", "func": "ves_icall_SignatureHelper_get_signature_field_raw", "handles": true, "flags": "4" }
	,{ "name": "get_signature_local", "func": "ves_icall_SignatureHelper_get_signature_local_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.FieldInfo", "icalls": [{} 	,{ "name": "get_marshal_info", "func": "ves_icall_System_Reflection_FieldInfo_get_marshal_info_raw", "handles": true, "flags": "4" }
	,{ "name": "internal_from_handle_type", "func": "ves_icall_System_Reflection_FieldInfo_internal_from_handle_type_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.LoaderAllocatorScout", "icalls": [{} 	,{ "name": "Destroy", "func": "ves_icall_System_Reflection_LoaderAllocatorScout_Destroy", "handles": false, "flags": "0" }
]},
 { "klass":"System.Reflection.Metadata.MetadataUpdater", "icalls": [{} 	,{ "name": "ApplyUpdateEnabled", "func": "ves_icall_AssemblyExtensions_ApplyUpdateEnabled", "handles": false, "flags": "0" }
	,{ "name": "ApplyUpdate_internal", "func": "ves_icall_AssemblyExtensions_ApplyUpdate", "handles": false, "flags": "0" }
	,{ "name": "GetApplyUpdateCapabilities", "func": "ves_icall_AssemblyExtensions_GetApplyUpdateCapabilities_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.MethodBase", "icalls": [{} 	,{ "name": "GetCurrentMethod", "func": "ves_icall_GetCurrentMethod_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.MonoMethodInfo", "icalls": [{} 	,{ "name": "get_method_attributes", "func": "ves_icall_get_method_attributes", "handles": false, "flags": "0" }
	,{ "name": "get_method_info", "func": "ves_icall_get_method_info_raw", "handles": true, "flags": "4" }
	,{ "name": "get_parameter_info", "func": "ves_icall_System_Reflection_MonoMethodInfo_get_parameter_info_raw", "handles": true, "flags": "4" }
	,{ "name": "get_retval_marshal", "func": "ves_icall_System_MonoMethodInfo_get_retval_marshal_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeAssembly", "icalls": [{} 	,{ "name": "GetEntryPoint", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetEntryPoint_raw", "handles": true, "flags": "4" }
	,{ "name": "GetExportedTypes", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetExportedTypes_raw", "handles": true, "flags": "4" }
	,{ "name": "GetInfo", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetInfo_raw", "handles": true, "flags": "4" }
	,{ "name": "GetManifestModuleInternal", "func": "ves_icall_System_Reflection_Assembly_GetManifestModuleInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetManifestResourceInfoInternal", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceInfoInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetManifestResourceInternal", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetManifestResourceNames", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceNames_raw", "handles": true, "flags": "4" }
	,{ "name": "GetModulesInternal", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetModulesInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetTopLevelForwardedTypes", "func": "ves_icall_System_Reflection_RuntimeAssembly_GetTopLevelForwardedTypes_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetReferencedAssemblies", "func": "ves_icall_System_Reflection_Assembly_InternalGetReferencedAssemblies_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalTryGetRawMetadata", "func": "ves_icall_System_Reflection_RuntimeAssembly_InternalTryGetRawMetadata_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeConstructorInfo", "icalls": [{} 	,{ "name": "GetGenericMethodDefinition_impl", "func": "ves_icall_RuntimeMethodInfo_GetGenericMethodDefinition_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalInvoke", "func": "ves_icall_InternalInvoke_raw", "handles": true, "flags": "4" }
	,{ "name": "InvokeClassConstructor", "func": "ves_icall_InvokeClassConstructor_raw", "handles": true, "flags": "4" }
	,{ "name": "get_metadata_token", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeCustomAttributeData", "icalls": [{} 	,{ "name": "ResolveArgumentsInternal", "func": "ves_icall_System_Reflection_RuntimeCustomAttributeData_ResolveArgumentsInternal_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeEventInfo", "icalls": [{} 	,{ "name": "get_event_info", "func": "ves_icall_RuntimeEventInfo_get_event_info_raw", "handles": true, "flags": "4" }
	,{ "name": "get_metadata_token", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
	,{ "name": "internal_from_handle_type", "func": "ves_icall_System_Reflection_EventInfo_internal_from_handle_type_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeFieldInfo", "icalls": [{} 	,{ "name": "GetFieldOffset", "func": "ves_icall_RuntimeFieldInfo_GetFieldOffset_raw", "handles": true, "flags": "4" }
	,{ "name": "GetParentType", "func": "ves_icall_RuntimeFieldInfo_GetParentType_raw", "handles": true, "flags": "4" }
	,{ "name": "GetRawConstantValue", "func": "ves_icall_RuntimeFieldInfo_GetRawConstantValue_raw", "handles": true, "flags": "4" }
	,{ "name": "GetTypeModifiers", "func": "ves_icall_System_Reflection_FieldInfo_GetTypeModifiers_raw", "handles": true, "flags": "4" }
	,{ "name": "GetValueInternal", "func": "ves_icall_RuntimeFieldInfo_GetValueInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveType", "func": "ves_icall_RuntimeFieldInfo_ResolveType_raw", "handles": true, "flags": "4" }
	,{ "name": "SetValueInternal", "func": "ves_icall_RuntimeFieldInfo_SetValueInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "UnsafeGetValue", "func": "ves_icall_RuntimeFieldInfo_GetValueInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "get_metadata_token", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeMethodInfo", "icalls": [{} 	,{ "name": "GetGenericArguments", "func": "ves_icall_RuntimeMethodInfo_GetGenericArguments_raw", "handles": true, "flags": "4" }
	,{ "name": "GetGenericMethodDefinition_impl", "func": "ves_icall_RuntimeMethodInfo_GetGenericMethodDefinition_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMethodBodyInternal", "func": "ves_icall_System_Reflection_RuntimeMethodInfo_GetMethodBodyInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMethodFromHandleInternalType_native", "func": "ves_icall_System_Reflection_RuntimeMethodInfo_GetMethodFromHandleInternalType_native_raw", "handles": true, "flags": "4" }
	,{ "name": "GetPInvoke", "func": "ves_icall_RuntimeMethodInfo_GetPInvoke_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalInvoke", "func": "ves_icall_InternalInvoke_raw", "handles": true, "flags": "4" }
	,{ "name": "MakeGenericMethod_impl", "func": "ves_icall_RuntimeMethodInfo_MakeGenericMethod_impl_raw", "handles": true, "flags": "4" }
	,{ "name": "get_IsGenericMethod", "func": "ves_icall_RuntimeMethodInfo_get_IsGenericMethod_raw", "handles": true, "flags": "4" }
	,{ "name": "get_IsGenericMethodDefinition", "func": "ves_icall_RuntimeMethodInfo_get_IsGenericMethodDefinition_raw", "handles": true, "flags": "4" }
	,{ "name": "get_base_method", "func": "ves_icall_RuntimeMethodInfo_get_base_method_raw", "handles": true, "flags": "4" }
	,{ "name": "get_metadata_token", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
	,{ "name": "get_name", "func": "ves_icall_RuntimeMethodInfo_get_name_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeModule", "icalls": [{} 	,{ "name": "GetGlobalType", "func": "ves_icall_System_Reflection_RuntimeModule_GetGlobalType_raw", "handles": true, "flags": "4" }
	,{ "name": "GetGuidInternal", "func": "ves_icall_System_Reflection_RuntimeModule_GetGuidInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMDStreamVersion", "func": "ves_icall_System_Reflection_RuntimeModule_GetMDStreamVersion_raw", "handles": true, "flags": "4" }
	,{ "name": "GetPEKind", "func": "ves_icall_System_Reflection_RuntimeModule_GetPEKind_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetTypes", "func": "ves_icall_System_Reflection_RuntimeModule_InternalGetTypes_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveFieldToken", "func": "ves_icall_System_Reflection_RuntimeModule_ResolveFieldToken_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveMemberToken", "func": "ves_icall_System_Reflection_RuntimeModule_ResolveMemberToken_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveMethodToken", "func": "ves_icall_System_Reflection_RuntimeModule_ResolveMethodToken_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveSignature", "func": "ves_icall_System_Reflection_RuntimeModule_ResolveSignature_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveStringToken", "func": "ves_icall_System_Reflection_RuntimeModule_ResolveStringToken_raw", "handles": true, "flags": "4" }
	,{ "name": "ResolveTypeToken", "func": "ves_icall_System_Reflection_RuntimeModule_ResolveTypeToken_raw", "handles": true, "flags": "4" }
	,{ "name": "get_MetadataToken", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimeParameterInfo", "icalls": [{} 	,{ "name": "GetMetadataToken", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
	,{ "name": "GetTypeModifiers", "func": "ves_icall_RuntimeParameterInfo_GetTypeModifiers_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Reflection.RuntimePropertyInfo", "icalls": [{} 	,{ "name": "GetTypeModifiers", "func": "ves_icall_RuntimePropertyInfo_GetTypeModifiers_raw", "handles": true, "flags": "4" }
	,{ "name": "get_default_value", "func": "ves_icall_property_info_get_default_value_raw", "handles": true, "flags": "4" }
	,{ "name": "get_metadata_token", "func": "ves_icall_reflection_get_token_raw", "handles": true, "flags": "4" }
	,{ "name": "get_property_info", "func": "ves_icall_RuntimePropertyInfo_get_property_info_raw", "handles": true, "flags": "4" }
	,{ "name": "internal_from_handle_type", "func": "ves_icall_System_Reflection_RuntimePropertyInfo_internal_from_handle_type_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Runtime.CompilerServices.RuntimeHelpers", "icalls": [{} 	,{ "name": "GetObjectValue", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetObjectValue_raw", "handles": true, "flags": "4" }
	,{ "name": "GetSpanDataFrom", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetSpanDataFrom_raw", "handles": true, "flags": "4" }
	,{ "name": "GetUninitializedObjectInternal", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetUninitializedObjectInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "InitializeArray", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalBox", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalBox_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetHashCode", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalGetHashCode_raw", "handles": true, "flags": "4" }
	,{ "name": "PrepareMethod", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_PrepareMethod_raw", "handles": true, "flags": "4" }
	,{ "name": "RunClassConstructor", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_RunClassConstructor_raw", "handles": true, "flags": "4" }
	,{ "name": "RunModuleConstructor", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_RunModuleConstructor_raw", "handles": true, "flags": "4" }
	,{ "name": "SizeOf", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_SizeOf_raw", "handles": true, "flags": "4" }
	,{ "name": "SufficientExecutionStack", "func": "ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_SufficientExecutionStack", "handles": false, "flags": "0" }
]},
 { "klass":"System.Runtime.InteropServices.GCHandle", "icalls": [{} 	,{ "name": "InternalAlloc", "func": "ves_icall_System_GCHandle_InternalAlloc_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalFree", "func": "ves_icall_System_GCHandle_InternalFree_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGet", "func": "ves_icall_System_GCHandle_InternalGet_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalSet", "func": "ves_icall_System_GCHandle_InternalSet_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Runtime.InteropServices.Marshal", "icalls": [{} 	,{ "name": "DestroyStructure", "func": "ves_icall_System_Runtime_InteropServices_Marshal_DestroyStructure_raw", "handles": true, "flags": "4" }
	,{ "name": "GetDelegateForFunctionPointerInternal", "func": "ves_icall_System_Runtime_InteropServices_Marshal_GetDelegateForFunctionPointerInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetFunctionPointerForDelegateInternal", "func": "ves_icall_System_Runtime_InteropServices_Marshal_GetFunctionPointerForDelegateInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetLastPInvokeError", "func": "ves_icall_System_Runtime_InteropServices_Marshal_GetLastPInvokeError", "handles": false, "flags": "0" }
	,{ "name": "OffsetOf", "func": "ves_icall_System_Runtime_InteropServices_Marshal_OffsetOf_raw", "handles": true, "flags": "4" }
	,{ "name": "PrelinkInternal", "func": "ves_icall_System_Runtime_InteropServices_Marshal_Prelink_raw", "handles": true, "flags": "4" }
	,{ "name": "PtrToStructureHelper", "func": "ves_icall_System_Runtime_InteropServices_Marshal_PtrToStructureHelper_raw", "handles": true, "flags": "4" }
	,{ "name": "SetLastPInvokeError", "func": "ves_icall_System_Runtime_InteropServices_Marshal_SetLastPInvokeError", "handles": false, "flags": "0" }
	,{ "name": "SizeOfHelper", "func": "ves_icall_System_Runtime_InteropServices_Marshal_SizeOfHelper_raw", "handles": true, "flags": "4" }
	,{ "name": "StructureToPtr", "func": "ves_icall_System_Runtime_InteropServices_Marshal_StructureToPtr_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Runtime.InteropServices.NativeLibrary", "icalls": [{} 	,{ "name": "FreeLib", "func": "ves_icall_System_Runtime_InteropServices_NativeLibrary_FreeLib_raw", "handles": true, "flags": "4" }
	,{ "name": "GetSymbol", "func": "ves_icall_System_Runtime_InteropServices_NativeLibrary_GetSymbol_raw", "handles": true, "flags": "4" }
	,{ "name": "LoadByName", "func": "ves_icall_System_Runtime_InteropServices_NativeLibrary_LoadByName_raw", "handles": true, "flags": "4" }
	,{ "name": "LoadFromPath", "func": "ves_icall_System_Runtime_InteropServices_NativeLibrary_LoadFromPath_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Runtime.Loader.AssemblyLoadContext", "icalls": [{} 	,{ "name": "GetLoadContextForAssembly", "func": "ves_icall_System_Runtime_Loader_AssemblyLoadContext_GetLoadContextForAssembly_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetLoadedAssemblies", "func": "ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalGetLoadedAssemblies_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalInitializeNativeALC", "func": "ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalInitializeNativeALC_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalLoadFile", "func": "ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFile_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalLoadFromStream", "func": "ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFromStream_raw", "handles": true, "flags": "4" }
	,{ "name": "PrepareForAssemblyLoadContextRelease", "func": "ves_icall_System_Runtime_Loader_AssemblyLoadContext_PrepareForAssemblyLoadContextRelease_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.RuntimeFieldHandle", "icalls": [{} 	,{ "name": "GetFieldDataReferenceInternal", "func": "ves_icall_System_RuntimeFieldHandle_GetFieldDataReference_raw", "handles": true, "flags": "4" }
	,{ "name": "GetValueDirect", "func": "ves_icall_System_RuntimeFieldHandle_GetValueDirect_raw", "handles": true, "flags": "4" }
	,{ "name": "SetValueDirect", "func": "ves_icall_System_RuntimeFieldHandle_SetValueDirect_raw", "handles": true, "flags": "4" }
	,{ "name": "SetValueInternal", "func": "ves_icall_RuntimeFieldInfo_SetValueInternal_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.RuntimeMethodHandle", "icalls": [{} 	,{ "name": "GetFunctionPointer", "func": "ves_icall_RuntimeMethodHandle_GetFunctionPointer_raw", "handles": true, "flags": "4" }
	,{ "name": "ReboxFromNullable", "func": "ves_icall_RuntimeMethodHandle_ReboxFromNullable_raw", "handles": true, "flags": "4" }
	,{ "name": "ReboxToNullable", "func": "ves_icall_RuntimeMethodHandle_ReboxToNullable_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.RuntimeType", "icalls": [{} 	,{ "name": "CreateInstanceInternal", "func": "ves_icall_System_RuntimeType_CreateInstanceInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "FunctionPointerReturnAndParameterTypes", "func": "ves_icall_RuntimeType_FunctionPointerReturnAndParameterTypes_raw", "handles": true, "flags": "4" }
	,{ "name": "GetCallingConventionFromFunctionPointerInternal", "func": "ves_icall_RuntimeType_GetCallingConventionFromFunctionPointerInternal", "handles": false, "flags": "0" }
	,{ "name": "GetConstructors_native", "func": "ves_icall_RuntimeType_GetConstructors_native_raw", "handles": true, "flags": "4" }
	,{ "name": "GetCorrespondingInflatedMethod", "func": "ves_icall_RuntimeType_GetCorrespondingInflatedMethod_raw", "handles": true, "flags": "4" }
	,{ "name": "GetDeclaringMethod", "func": "ves_icall_RuntimeType_GetDeclaringMethod_raw", "handles": true, "flags": "4" }
	,{ "name": "GetDeclaringType", "func": "ves_icall_RuntimeType_GetDeclaringType_raw", "handles": true, "flags": "4" }
	,{ "name": "GetEvents_native", "func": "ves_icall_RuntimeType_GetEvents_native_raw", "handles": true, "flags": "4" }
	,{ "name": "GetFields_native", "func": "ves_icall_RuntimeType_GetFields_native_raw", "handles": true, "flags": "4" }
	,{ "name": "GetFunctionPointerTypeModifiers", "func": "ves_icall_RuntimeType_GetFunctionPointerTypeModifiers_raw", "handles": true, "flags": "4" }
	,{ "name": "GetGenericArgumentsInternal", "func": "ves_icall_RuntimeType_GetGenericArgumentsInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetGenericParameterPosition", "func": "ves_icall_RuntimeType_GetGenericParameterPosition", "handles": false, "flags": "0" }
	,{ "name": "GetInterfaceMapData", "func": "ves_icall_RuntimeType_GetInterfaceMapData_raw", "handles": true, "flags": "4" }
	,{ "name": "GetInterfaces", "func": "ves_icall_RuntimeType_GetInterfaces_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMethodsByName_native", "func": "ves_icall_RuntimeType_GetMethodsByName_native_raw", "handles": true, "flags": "4" }
	,{ "name": "GetName", "func": "ves_icall_RuntimeType_GetName_raw", "handles": true, "flags": "4" }
	,{ "name": "GetNamespace", "func": "ves_icall_RuntimeType_GetNamespace_raw", "handles": true, "flags": "4" }
	,{ "name": "GetNestedTypes_native", "func": "ves_icall_RuntimeType_GetNestedTypes_native_raw", "handles": true, "flags": "4" }
	,{ "name": "GetPacking", "func": "ves_icall_RuntimeType_GetPacking_raw", "handles": true, "flags": "4" }
	,{ "name": "GetParentType", "func": "ves_icall_RuntimeType_GetParentType_raw", "handles": true, "flags": "4" }
	,{ "name": "GetPropertiesByName_native", "func": "ves_icall_RuntimeType_GetPropertiesByName_native_raw", "handles": true, "flags": "4" }
	,{ "name": "IsUnmanagedFunctionPointerInternal", "func": "ves_icall_RuntimeType_IsUnmanagedFunctionPointerInternal", "handles": false, "flags": "0" }
	,{ "name": "MakeGenericType", "func": "ves_icall_RuntimeType_MakeGenericType_raw", "handles": true, "flags": "4" }
	,{ "name": "getFullName", "func": "ves_icall_System_RuntimeType_getFullName_raw", "handles": true, "flags": "4" }
	,{ "name": "make_array_type", "func": "ves_icall_RuntimeType_make_array_type_raw", "handles": true, "flags": "4" }
	,{ "name": "make_byref_type", "func": "ves_icall_RuntimeType_make_byref_type_raw", "handles": true, "flags": "4" }
	,{ "name": "make_pointer_type", "func": "ves_icall_RuntimeType_make_pointer_type_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.RuntimeTypeHandle", "icalls": [{} 	,{ "name": "GetArrayRank", "func": "ves_icall_RuntimeTypeHandle_GetArrayRank_raw", "handles": true, "flags": "4" }
	,{ "name": "GetAssembly", "func": "ves_icall_RuntimeTypeHandle_GetAssembly_raw", "handles": true, "flags": "4" }
	,{ "name": "GetAttributes", "func": "ves_icall_RuntimeTypeHandle_GetAttributes", "handles": false, "flags": "0" }
	,{ "name": "GetCorElementType", "func": "ves_icall_RuntimeTypeHandle_GetCorElementType", "handles": false, "flags": "0" }
	,{ "name": "GetElementType", "func": "ves_icall_RuntimeTypeHandle_GetElementType_raw", "handles": true, "flags": "4" }
	,{ "name": "GetGenericParameterInfo", "func": "ves_icall_RuntimeTypeHandle_GetGenericParameterInfo_raw", "handles": true, "flags": "4" }
	,{ "name": "GetGenericTypeDefinition_impl", "func": "ves_icall_RuntimeTypeHandle_GetGenericTypeDefinition_impl_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMetadataToken", "func": "ves_icall_RuntimeTypeHandle_GetMetadataToken_raw", "handles": true, "flags": "4" }
	,{ "name": "GetModule", "func": "ves_icall_RuntimeTypeHandle_GetModule_raw", "handles": true, "flags": "4" }
	,{ "name": "GetMonoClass", "func": "ves_icall_RuntimeTypeHandle_GetMonoClass_raw", "handles": true, "flags": "4" }
	,{ "name": "HasInstantiation", "func": "ves_icall_RuntimeTypeHandle_HasInstantiation", "handles": false, "flags": "0" }
	,{ "name": "HasReferences", "func": "ves_icall_RuntimeTypeHandle_HasReferences_raw", "handles": true, "flags": "4" }
	,{ "name": "IsByRefLike", "func": "ves_icall_RuntimeTypeHandle_IsByRefLike_raw", "handles": true, "flags": "4" }
	,{ "name": "IsGenericTypeDefinition", "func": "ves_icall_RuntimeTypeHandle_IsGenericTypeDefinition", "handles": false, "flags": "0" }
	,{ "name": "IsInstanceOfType", "func": "ves_icall_RuntimeTypeHandle_IsInstanceOfType_raw", "handles": true, "flags": "4" }
	,{ "name": "internal_from_name", "func": "ves_icall_System_RuntimeTypeHandle_internal_from_name_raw", "handles": true, "flags": "4" }
	,{ "name": "is_subclass_of", "func": "ves_icall_RuntimeTypeHandle_is_subclass_of_raw", "handles": true, "flags": "4" }
	,{ "name": "type_is_assignable_from", "func": "ves_icall_RuntimeTypeHandle_type_is_assignable_from_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.String", "icalls": [{} 	,{ "name": ".ctor(System.ReadOnlySpan`1<char>)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(char*)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(char*,int,int)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(char,int)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(char[])", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(char[],int,int)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(sbyte*)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(sbyte*,int,int)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": ".ctor(sbyte*,int,int,System.Text.Encoding)", "func": "ves_icall_System_String_ctor_RedirectToCreateString", "handles": false, "flags": "0" }
	,{ "name": "FastAllocateString", "func": "ves_icall_System_String_FastAllocateString_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalIntern", "func": "ves_icall_System_String_InternalIntern_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalIsInterned", "func": "ves_icall_System_String_InternalIsInterned_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Threading.Interlocked", "icalls": [{} 	,{ "name": "Add(int&,int)", "func": "ves_icall_System_Threading_Interlocked_Add_Int", "handles": false, "flags": "0" }
	,{ "name": "Add(long&,long)", "func": "ves_icall_System_Threading_Interlocked_Add_Long", "handles": false, "flags": "0" }
	,{ "name": "CompareExchange(int&,int,int)", "func": "ves_icall_System_Threading_Interlocked_CompareExchange_Int", "handles": false, "flags": "0" }
	,{ "name": "CompareExchange(long&,long,long)", "func": "ves_icall_System_Threading_Interlocked_CompareExchange_Long", "handles": false, "flags": "0" }
	,{ "name": "CompareExchange(object&,object&,object&,object&)", "func": "ves_icall_System_Threading_Interlocked_CompareExchange_Object", "handles": false, "flags": "0" }
	,{ "name": "Decrement(int&)", "func": "ves_icall_System_Threading_Interlocked_Decrement_Int", "handles": false, "flags": "0" }
	,{ "name": "Decrement(long&)", "func": "ves_icall_System_Threading_Interlocked_Decrement_Long", "handles": false, "flags": "0" }
	,{ "name": "Exchange(int&,int)", "func": "ves_icall_System_Threading_Interlocked_Exchange_Int", "handles": false, "flags": "0" }
	,{ "name": "Exchange(long&,long)", "func": "ves_icall_System_Threading_Interlocked_Exchange_Long", "handles": false, "flags": "0" }
	,{ "name": "Exchange(object&,object&,object&)", "func": "ves_icall_System_Threading_Interlocked_Exchange_Object", "handles": false, "flags": "0" }
	,{ "name": "Increment(int&)", "func": "ves_icall_System_Threading_Interlocked_Increment_Int", "handles": false, "flags": "0" }
	,{ "name": "Increment(long&)", "func": "ves_icall_System_Threading_Interlocked_Increment_Long", "handles": false, "flags": "0" }
	,{ "name": "MemoryBarrierProcessWide", "func": "ves_icall_System_Threading_Interlocked_MemoryBarrierProcessWide", "handles": false, "flags": "0" }
	,{ "name": "Read(long&)", "func": "ves_icall_System_Threading_Interlocked_Read_Long", "handles": false, "flags": "0" }
]},
 { "klass":"System.Threading.LowLevelLifoSemaphore", "icalls": [{} 	,{ "name": "DeleteInternal", "func": "ves_icall_System_Threading_LowLevelLifoSemaphore_DeleteInternal", "handles": false, "flags": "0" }
	,{ "name": "InitInternal", "func": "ves_icall_System_Threading_LowLevelLifoSemaphore_InitInternal", "handles": false, "flags": "0" }
	,{ "name": "ReleaseInternal", "func": "ves_icall_System_Threading_LowLevelLifoSemaphore_ReleaseInternal", "handles": false, "flags": "0" }
	,{ "name": "TimedWaitInternal", "func": "ves_icall_System_Threading_LowLevelLifoSemaphore_TimedWaitInternal", "handles": false, "flags": "0" }
]},
 { "klass":"System.Threading.Monitor", "icalls": [{} 	,{ "name": "Enter", "func": "ves_icall_System_Threading_Monitor_Monitor_Enter_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalExit", "func": "mono_monitor_exit_icall_raw", "handles": true, "flags": "4" }
	,{ "name": "Monitor_get_lock_contention_count", "func": "ves_icall_System_Threading_Monitor_Monitor_get_lock_contention_count", "handles": false, "flags": "0" }
	,{ "name": "Monitor_pulse", "func": "ves_icall_System_Threading_Monitor_Monitor_pulse_raw", "handles": true, "flags": "4" }
	,{ "name": "Monitor_pulse_all", "func": "ves_icall_System_Threading_Monitor_Monitor_pulse_all_raw", "handles": true, "flags": "4" }
	,{ "name": "Monitor_wait", "func": "ves_icall_System_Threading_Monitor_Monitor_wait_raw", "handles": true, "flags": "4" }
	,{ "name": "try_enter_with_atomic_var", "func": "ves_icall_System_Threading_Monitor_Monitor_try_enter_with_atomic_var_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.Threading.Thread", "icalls": [{} 	,{ "name": "ClrState", "func": "ves_icall_System_Threading_Thread_ClrState_raw", "handles": true, "flags": "4" }
	,{ "name": "FreeInternal", "func": "ves_icall_System_Threading_InternalThread_Thread_free_internal_raw", "handles": true, "flags": "4" }
	,{ "name": "GetCurrentOSThreadId", "func": "ves_icall_System_Threading_Thread_GetCurrentOSThreadId_raw", "handles": true, "flags": "4" }
	,{ "name": "GetCurrentThread", "func": "ves_icall_System_Threading_Thread_GetCurrentThread", "handles": false, "flags": "0" }
	,{ "name": "GetState", "func": "ves_icall_System_Threading_Thread_GetState_raw", "handles": true, "flags": "4" }
	,{ "name": "InitInternal", "func": "ves_icall_System_Threading_Thread_InitInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "InterruptInternal", "func": "ves_icall_System_Threading_Thread_Interrupt_internal_raw", "handles": true, "flags": "4" }
	,{ "name": "JoinInternal", "func": "ves_icall_System_Threading_Thread_Join_internal_raw", "handles": true, "flags": "4" }
	,{ "name": "SetName_icall", "func": "ves_icall_System_Threading_Thread_SetName_icall_raw", "handles": true, "flags": "4" }
	,{ "name": "SetPriority", "func": "ves_icall_System_Threading_Thread_SetPriority_raw", "handles": true, "flags": "4" }
	,{ "name": "SetState", "func": "ves_icall_System_Threading_Thread_SetState_raw", "handles": true, "flags": "4" }
	,{ "name": "StartInternal", "func": "ves_icall_System_Threading_Thread_StartInternal_raw", "handles": true, "flags": "4" }
	,{ "name": "YieldInternal", "func": "ves_icall_System_Threading_Thread_YieldInternal", "handles": false, "flags": "0" }
]},
 { "klass":"System.Type", "icalls": [{} 	,{ "name": "internal_from_handle", "func": "ves_icall_System_Type_internal_from_handle_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.TypedReference", "icalls": [{} 	,{ "name": "InternalToObject", "func": "ves_icall_System_TypedReference_ToObject_raw", "handles": true, "flags": "4" }
]},
 { "klass":"System.ValueType", "icalls": [{} 	,{ "name": "InternalEquals", "func": "ves_icall_System_ValueType_Equals_raw", "handles": true, "flags": "4" }
	,{ "name": "InternalGetHashCode", "func": "ves_icall_System_ValueType_InternalGetHashCode_raw", "handles": true, "flags": "4" }
]}
]
