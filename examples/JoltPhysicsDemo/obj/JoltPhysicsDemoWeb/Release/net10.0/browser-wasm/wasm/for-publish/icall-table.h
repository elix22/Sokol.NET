
#define ICALL_TABLE_corlib 1

static int corlib_icall_indexes [] = {
    /* 0 */ 131,
    /* 1 */ 138,
    /* 2 */ 139,
    /* 3 */ 140,
    /* 4 */ 141,
    /* 5 */ 142,
    /* 6 */ 143,
    /* 7 */ 144,
    /* 8 */ 146,
    /* 9 */ 172,
    /* 10 */ 173,
    /* 11 */ 174,
    /* 12 */ 192,
    /* 13 */ 193,
    /* 14 */ 196,
    /* 15 */ 197,
    /* 16 */ 198,
    /* 17 */ 258,
    /* 18 */ 259,
    /* 19 */ 262,
    /* 20 */ 291,
    /* 21 */ 292,
    /* 22 */ 293,
    /* 23 */ 294,
    /* 24 */ 298,
    /* 25 */ 299,
    /* 26 */ 301,
    /* 27 */ 305,
    /* 28 */ 307,
    /* 29 */ 312,
    /* 30 */ 320,
    /* 31 */ 321,
    /* 32 */ 322,
    /* 33 */ 323,
    /* 34 */ 324,
    /* 35 */ 325,
    /* 36 */ 326,
    /* 37 */ 367,
    /* 38 */ 368,
    /* 39 */ 369,
    /* 40 */ 370,
    /* 41 */ 371,
    /* 42 */ 373,
    /* 43 */ 374,
    /* 44 */ 400,
    /* 45 */ 407,
    /* 46 */ 408,
    /* 47 */ 412,
    /* 48 */ 461,
    /* 49 */ 466,
    /* 50 */ 469,
    /* 51 */ 471,
    /* 52 */ 476,
    /* 53 */ 477,
    /* 54 */ 479,
    /* 55 */ 480,
    /* 56 */ 484,
    /* 57 */ 485,
    /* 58 */ 487,
    /* 59 */ 488,
    /* 60 */ 491,
    /* 61 */ 492,
    /* 62 */ 493,
    /* 63 */ 496,
    /* 64 */ 498,
    /* 65 */ 500,
    /* 66 */ 502,
    /* 67 */ 511,
    /* 68 */ 563,
    /* 69 */ 565,
    /* 70 */ 567,
    /* 71 */ 577,
    /* 72 */ 578,
    /* 73 */ 579,
    /* 74 */ 581,
    /* 75 */ 584,
    /* 76 */ 585,
    /* 77 */ 586,
    /* 78 */ 587,
    /* 79 */ 594,
    /* 80 */ 595,
    /* 81 */ 596,
    /* 82 */ 600,
    /* 83 */ 601,
    /* 84 */ 603,
    /* 85 */ 718,
    /* 86 */ 860,
    /* 87 */ 861,
    /* 88 */ 4199,
    /* 89 */ 4200,
    /* 90 */ 4202,
    /* 91 */ 4203,
    /* 92 */ 4204,
    /* 93 */ 4205,
    /* 94 */ 4207,
    /* 95 */ 4208,
    /* 96 */ 4209,
    /* 97 */ 4221,
    /* 98 */ 4223,
    /* 99 */ 4227,
    /* 100 */ 4229,
    /* 101 */ 4231,
    /* 102 */ 4282,
    /* 103 */ 4283,
    /* 104 */ 4285,
    /* 105 */ 4286,
    /* 106 */ 4287,
    /* 107 */ 4288,
    /* 108 */ 4289,
    /* 109 */ 4291,
    /* 110 */ 4293,
    /* 111 */ 4770,
    /* 112 */ 4773,
    /* 113 */ 4775,
    /* 114 */ 4776,
    /* 115 */ 4777,
    /* 116 */ 4914,
    /* 117 */ 4915,
    /* 118 */ 4916,
    /* 119 */ 4936,
    /* 120 */ 4937,
    /* 121 */ 4938,
    /* 122 */ 4940,
    /* 123 */ 5059,
    /* 124 */ 5069,
    /* 125 */ 5070,
    /* 126 */ 5071,
    /* 127 */ 5072,
    /* 128 */ 5073,
    /* 129 */ 5199,
    /* 130 */ 5201,
    /* 131 */ 5221,
    /* 132 */ 5235,
    /* 133 */ 5241,
    /* 134 */ 5248,
    /* 135 */ 5259,
    /* 136 */ 5262,
    /* 137 */ 5278,
    /* 138 */ 5349,
    /* 139 */ 5351,
    /* 140 */ 5357,
    /* 141 */ 5365,
    /* 142 */ 5383,
    /* 143 */ 5384,
    /* 144 */ 5392,
    /* 145 */ 5394,
    /* 146 */ 5400,
    /* 147 */ 5401,
    /* 148 */ 5404,
    /* 149 */ 5408,
    /* 150 */ 5414,
    /* 151 */ 5415,
    /* 152 */ 5422,
    /* 153 */ 5424,
    /* 154 */ 5435,
    /* 155 */ 5438,
    /* 156 */ 5439,
    /* 157 */ 5440,
    /* 158 */ 5450,
    /* 159 */ 5459,
    /* 160 */ 5464,
    /* 161 */ 5465,
    /* 162 */ 5466,
    /* 163 */ 5482,
    /* 164 */ 5484,
    /* 165 */ 5497,
    /* 166 */ 5531,
    /* 167 */ 5553,
    /* 168 */ 5554,
    /* 169 */ 5937,
    /* 170 */ 5991,
    /* 171 */ 5992,
    /* 172 */ 6121,
    /* 173 */ 6122,
    /* 174 */ 6126,
    /* 175 */ 6129,
    /* 176 */ 6168,
    /* 177 */ 6653,
    /* 178 */ 6657,
    /* 179 */ 6667,
    /* 180 */ 6712,
    /* 181 */ 6713,
    /* 182 */ 7047,
    /* 183 */ 7068,
    /* 184 */ 7070,
    /* 185 */ 7072
};

void ves_icall_System_Array_InternalCreate (int, int, int, int, int); 
int ves_icall_System_Array_GetCorElementTypeOfElementTypeInternal (int); 
int ves_icall_System_Array_CanChangePrimitive (int, int, int); 
int ves_icall_System_Array_FastCopy (int, int, int, int, int); 
int ves_icall_System_Array_GetLengthInternal_raw (int, int, int); 
int ves_icall_System_Array_GetLowerBoundInternal_raw (int, int, int); 
void ves_icall_System_Array_GetGenericValue_icall (int, int, int); 
void ves_icall_System_Array_GetValueImpl_raw (int, int, int, int); 
void ves_icall_System_Array_SetValueRelaxedImpl_raw (int, int, int, int); 
void ves_icall_System_Runtime_RuntimeImports_ZeroMemory (int, int); 
void ves_icall_System_Runtime_RuntimeImports_Memmove (int, int, int); 
void ves_icall_System_Buffer_BulkMoveWithWriteBarrier (int, int, int, int); 
int ves_icall_System_Delegate_CreateDelegate_internal_raw (int, int, int, int, int); 
int ves_icall_System_Delegate_GetVirtualMethod_internal_raw (int, int); 
void ves_icall_System_Enum_GetEnumValuesAndNames_raw (int, int, int, int); 
int ves_icall_System_Enum_InternalGetCorElementType (int); 
void ves_icall_System_Enum_InternalGetUnderlyingType_raw (int, int, int); 
int ves_icall_System_Environment_get_ProcessorCount (); 
void ves_icall_System_Environment_Exit (int); 
void ves_icall_System_Environment_FailFast_raw (int, int, int, int); 
int ves_icall_System_GC_GetMaxGeneration (); 
void ves_icall_System_GC_InternalCollect (int); 
void ves_icall_System_GC_register_ephemeron_array_raw (int, int); 
int ves_icall_System_GC_get_ephemeron_tombstone_raw (int); 
void ves_icall_System_GC_WaitForPendingFinalizers (); 
void ves_icall_System_GC_SuppressFinalize_raw (int, int); 
void ves_icall_System_GC_ReRegisterForFinalize_raw (int, int); 
void ves_icall_System_GC_GetGCMemoryInfo (int, int, int, int, int, int); 
int ves_icall_System_GC_AllocPinnedArray_raw (int, int, int); 
int ves_icall_System_Object_MemberwiseClone_raw (int, int); 
double ves_icall_System_Math_Asin (double); 
double ves_icall_System_Math_Atan2 (double, double); 
double ves_icall_System_Math_Ceiling (double); 
double ves_icall_System_Math_Cos (double); 
double ves_icall_System_Math_Sin (double); 
double ves_icall_System_Math_Sqrt (double); 
double ves_icall_System_Math_ModF (double, int); 
float ves_icall_System_MathF_Atan2 (float, float); 
float ves_icall_System_MathF_Cos (float); 
float ves_icall_System_MathF_Floor (float); 
float ves_icall_System_MathF_Pow (float, float); 
float ves_icall_System_MathF_Sin (float); 
float ves_icall_System_MathF_Sqrt (float); 
float ves_icall_System_MathF_Tan (float); 
int ves_icall_RuntimeMethodHandle_GetFunctionPointer_raw (int, int); 
void ves_icall_RuntimeMethodHandle_ReboxFromNullable_raw (int, int, int); 
void ves_icall_RuntimeMethodHandle_ReboxToNullable_raw (int, int, int, int); 
void ves_icall_RuntimeType_GetParentType_raw (int, int, int); 
int ves_icall_RuntimeType_GetCorrespondingInflatedMethod_raw (int, int, int); 
void ves_icall_RuntimeType_make_array_type_raw (int, int, int, int); 
void ves_icall_RuntimeType_make_byref_type_raw (int, int, int); 
void ves_icall_RuntimeType_make_pointer_type_raw (int, int, int); 
void ves_icall_RuntimeType_MakeGenericType_raw (int, int, int, int); 
int ves_icall_RuntimeType_GetMethodsByName_native_raw (int, int, int, int, int); 
int ves_icall_RuntimeType_GetPropertiesByName_native_raw (int, int, int, int, int); 
int ves_icall_RuntimeType_GetConstructors_native_raw (int, int, int); 
int ves_icall_System_RuntimeType_CreateInstanceInternal_raw (int, int); 
void ves_icall_RuntimeType_GetDeclaringMethod_raw (int, int, int); 
void ves_icall_System_RuntimeType_getFullName_raw (int, int, int, int, int); 
void ves_icall_RuntimeType_GetGenericArgumentsInternal_raw (int, int, int, int); 
int ves_icall_RuntimeType_GetGenericParameterPosition (int); 
int ves_icall_RuntimeType_GetEvents_native_raw (int, int, int, int); 
int ves_icall_RuntimeType_GetFields_native_raw (int, int, int, int, int); 
void ves_icall_RuntimeType_GetInterfaces_raw (int, int, int); 
void ves_icall_RuntimeType_GetDeclaringType_raw (int, int, int); 
void ves_icall_RuntimeType_GetName_raw (int, int, int); 
void ves_icall_RuntimeType_GetNamespace_raw (int, int, int); 
int ves_icall_RuntimeType_FunctionPointerReturnAndParameterTypes_raw (int, int); 
int ves_icall_RuntimeTypeHandle_GetAttributes (int); 
int ves_icall_RuntimeTypeHandle_GetMetadataToken_raw (int, int); 
void ves_icall_RuntimeTypeHandle_GetGenericTypeDefinition_impl_raw (int, int, int); 
int ves_icall_RuntimeTypeHandle_GetCorElementType (int); 
int ves_icall_RuntimeTypeHandle_HasInstantiation (int); 
int ves_icall_RuntimeTypeHandle_IsInstanceOfType_raw (int, int, int); 
int ves_icall_RuntimeTypeHandle_HasReferences_raw (int, int); 
int ves_icall_RuntimeTypeHandle_GetArrayRank_raw (int, int); 
void ves_icall_RuntimeTypeHandle_GetAssembly_raw (int, int, int); 
void ves_icall_RuntimeTypeHandle_GetElementType_raw (int, int, int); 
void ves_icall_RuntimeTypeHandle_GetModule_raw (int, int, int); 
int ves_icall_RuntimeTypeHandle_type_is_assignable_from_raw (int, int, int); 
int ves_icall_RuntimeTypeHandle_IsGenericTypeDefinition (int); 
int ves_icall_RuntimeTypeHandle_GetGenericParameterInfo_raw (int, int); 
int ves_icall_RuntimeTypeHandle_is_subclass_of_raw (int, int, int); 
int ves_icall_RuntimeTypeHandle_IsByRefLike_raw (int, int); 
int ves_icall_System_String_FastAllocateString_raw (int, int); 
int ves_icall_System_Type_internal_from_handle_raw (int, int); 
int ves_icall_System_ValueType_InternalGetHashCode_raw (int, int, int); 
int ves_icall_System_ValueType_Equals_raw (int, int, int, int); 
int ves_icall_System_Threading_Interlocked_CompareExchange_Int (int, int, int); 
void ves_icall_System_Threading_Interlocked_CompareExchange_Object (int, int, int, int); 
int ves_icall_System_Threading_Interlocked_Decrement_Int (int); 
int ves_icall_System_Threading_Interlocked_Increment_Int (int); 
int ves_icall_System_Threading_Interlocked_Exchange_Int (int, int); 
void ves_icall_System_Threading_Interlocked_Exchange_Object (int, int, int); 
int64_t ves_icall_System_Threading_Interlocked_CompareExchange_Long (int, int64_t, int64_t); 
int64_t ves_icall_System_Threading_Interlocked_Exchange_Long (int, int64_t); 
int ves_icall_System_Threading_Interlocked_Add_Int (int, int); 
void ves_icall_System_Threading_Monitor_Monitor_Enter_raw (int, int); 
void mono_monitor_exit_icall_raw (int, int); 
void ves_icall_System_Threading_Monitor_Monitor_pulse_all_raw (int, int); 
int ves_icall_System_Threading_Monitor_Monitor_wait_raw (int, int, int, int); 
void ves_icall_System_Threading_Monitor_Monitor_try_enter_with_atomic_var_raw (int, int, int, int, int); 
void ves_icall_System_Threading_Thread_InitInternal_raw (int, int); 
int ves_icall_System_Threading_Thread_GetCurrentThread (); 
void ves_icall_System_Threading_InternalThread_Thread_free_internal_raw (int, int); 
int ves_icall_System_Threading_Thread_GetState_raw (int, int); 
void ves_icall_System_Threading_Thread_SetState_raw (int, int, int); 
void ves_icall_System_Threading_Thread_ClrState_raw (int, int, int); 
void ves_icall_System_Threading_Thread_SetName_icall_raw (int, int, int, int); 
int ves_icall_System_Threading_Thread_YieldInternal (); 
void ves_icall_System_Threading_Thread_SetPriority_raw (int, int, int); 
void ves_icall_System_Runtime_Loader_AssemblyLoadContext_PrepareForAssemblyLoadContextRelease_raw (int, int, int); 
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_GetLoadContextForAssembly_raw (int, int); 
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFile_raw (int, int, int, int); 
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalInitializeNativeALC_raw (int, int, int, int, int); 
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFromStream_raw (int, int, int, int, int, int); 
int ves_icall_System_GCHandle_InternalAlloc_raw (int, int, int); 
void ves_icall_System_GCHandle_InternalFree_raw (int, int); 
int ves_icall_System_GCHandle_InternalGet_raw (int, int); 
int ves_icall_System_Runtime_InteropServices_Marshal_GetLastPInvokeError (); 
void ves_icall_System_Runtime_InteropServices_Marshal_SetLastPInvokeError (int); 
void ves_icall_System_Runtime_InteropServices_Marshal_StructureToPtr_raw (int, int, int, int); 
int ves_icall_System_Runtime_InteropServices_Marshal_SizeOfHelper_raw (int, int, int); 
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalGetHashCode_raw (int, int); 
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetUninitializedObjectInternal_raw (int, int); 
void ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray_raw (int, int, int); 
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetSpanDataFrom_raw (int, int, int, int); 
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_SufficientExecutionStack (); 
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalBox_raw (int, int, int); 
int ves_icall_System_Reflection_Assembly_GetEntryAssembly_raw (int); 
int ves_icall_System_Reflection_Assembly_InternalLoad_raw (int, int, int, int); 
int ves_icall_System_Reflection_AssemblyName_GetNativeName (int); 
int ves_icall_MonoCustomAttrs_GetCustomAttributesInternal_raw (int, int, int, int); 
int ves_icall_MonoCustomAttrs_GetCustomAttributesDataInternal_raw (int, int); 
int ves_icall_MonoCustomAttrs_IsDefinedInternal_raw (int, int, int); 
int ves_icall_System_Reflection_FieldInfo_internal_from_handle_type_raw (int, int, int); 
int ves_icall_System_Reflection_FieldInfo_get_marshal_info_raw (int, int); 
int ves_icall_System_Reflection_LoaderAllocatorScout_Destroy (int); 
void ves_icall_System_Reflection_RuntimeAssembly_GetInfo_raw (int, int, int, int); 
void ves_icall_System_Reflection_Assembly_GetManifestModuleInternal_raw (int, int, int); 
void ves_icall_System_Reflection_RuntimeCustomAttributeData_ResolveArgumentsInternal_raw (int, int, int, int, int, int, int); 
void ves_icall_RuntimeEventInfo_get_event_info_raw (int, int, int); 
int ves_icall_reflection_get_token_raw (int, int); 
int ves_icall_System_Reflection_EventInfo_internal_from_handle_type_raw (int, int, int); 
int ves_icall_RuntimeFieldInfo_ResolveType_raw (int, int); 
int ves_icall_RuntimeFieldInfo_GetParentType_raw (int, int, int); 
int ves_icall_RuntimeFieldInfo_GetFieldOffset_raw (int, int); 
int ves_icall_RuntimeFieldInfo_GetValueInternal_raw (int, int, int); 
int ves_icall_RuntimeFieldInfo_GetRawConstantValue_raw (int, int); 
int ves_icall_reflection_get_token_raw (int, int); 
void ves_icall_get_method_info_raw (int, int, int); 
int ves_icall_get_method_attributes (int); 
int ves_icall_System_Reflection_MonoMethodInfo_get_parameter_info_raw (int, int, int); 
int ves_icall_System_MonoMethodInfo_get_retval_marshal_raw (int, int); 
int ves_icall_System_Reflection_RuntimeMethodInfo_GetMethodFromHandleInternalType_native_raw (int, int, int, int); 
int ves_icall_RuntimeMethodInfo_get_name_raw (int, int); 
int ves_icall_RuntimeMethodInfo_get_base_method_raw (int, int, int); 
int ves_icall_reflection_get_token_raw (int, int); 
int ves_icall_InternalInvoke_raw (int, int, int, int, int); 
void ves_icall_RuntimeMethodInfo_GetPInvoke_raw (int, int, int, int, int); 
int ves_icall_RuntimeMethodInfo_GetGenericArguments_raw (int, int); 
int ves_icall_RuntimeMethodInfo_get_IsGenericMethodDefinition_raw (int, int); 
int ves_icall_RuntimeMethodInfo_get_IsGenericMethod_raw (int, int); 
void ves_icall_InvokeClassConstructor_raw (int, int); 
int ves_icall_InternalInvoke_raw (int, int, int, int, int); 
int ves_icall_reflection_get_token_raw (int, int); 
void ves_icall_RuntimePropertyInfo_get_property_info_raw (int, int, int, int); 
int ves_icall_reflection_get_token_raw (int, int); 
int ves_icall_System_Reflection_RuntimePropertyInfo_internal_from_handle_type_raw (int, int, int); 
void ves_icall_DynamicMethod_create_dynamic_method_raw (int, int, int, int, int); 
void ves_icall_AssemblyBuilder_basic_init_raw (int, int); 
void ves_icall_AssemblyBuilder_UpdateNativeCustomAttributes_raw (int, int); 
void ves_icall_ModuleBuilder_basic_init_raw (int, int); 
void ves_icall_ModuleBuilder_set_wrappers_type_raw (int, int, int); 
int ves_icall_ModuleBuilder_getToken_raw (int, int, int, int); 
void ves_icall_ModuleBuilder_RegisterToken_raw (int, int, int, int); 
int ves_icall_TypeBuilder_create_runtime_class_raw (int, int); 
int ves_icall_System_Diagnostics_Debugger_IsAttached_internal (); 
int ves_icall_System_Diagnostics_StackFrame_GetFrameInfo (int, int, int, int, int, int, int, int); 
void ves_icall_System_Diagnostics_StackTrace_GetTrace (int, int, int, int); 
void ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogWaitHandleWaitStart (int, int, int); 
void ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogWaitHandleWaitStop (int); 
int ves_icall_Mono_RuntimeClassHandle_GetTypeFromClass (int); 
void ves_icall_Mono_RuntimeGPtrArrayHandle_GPtrArrayFree (int); 
int ves_icall_Mono_SafeStringMarshal_StringToUtf8 (int); 
void ves_icall_Mono_SafeStringMarshal_GFree (int);

static void *corlib_icall_funcs [] = {
    /* 0:131 */ ves_icall_System_Array_InternalCreate,
    /* 1:138 */ ves_icall_System_Array_GetCorElementTypeOfElementTypeInternal,
    /* 2:139 */ ves_icall_System_Array_CanChangePrimitive,
    /* 3:140 */ ves_icall_System_Array_FastCopy,
    /* 4:141 */ ves_icall_System_Array_GetLengthInternal_raw,
    /* 5:142 */ ves_icall_System_Array_GetLowerBoundInternal_raw,
    /* 6:143 */ ves_icall_System_Array_GetGenericValue_icall,
    /* 7:144 */ ves_icall_System_Array_GetValueImpl_raw,
    /* 8:146 */ ves_icall_System_Array_SetValueRelaxedImpl_raw,
    /* 9:172 */ ves_icall_System_Runtime_RuntimeImports_ZeroMemory,
    /* 10:173 */ ves_icall_System_Runtime_RuntimeImports_Memmove,
    /* 11:174 */ ves_icall_System_Buffer_BulkMoveWithWriteBarrier,
    /* 12:192 */ ves_icall_System_Delegate_CreateDelegate_internal_raw,
    /* 13:193 */ ves_icall_System_Delegate_GetVirtualMethod_internal_raw,
    /* 14:196 */ ves_icall_System_Enum_GetEnumValuesAndNames_raw,
    /* 15:197 */ ves_icall_System_Enum_InternalGetCorElementType,
    /* 16:198 */ ves_icall_System_Enum_InternalGetUnderlyingType_raw,
    /* 17:258 */ ves_icall_System_Environment_get_ProcessorCount,
    /* 18:259 */ ves_icall_System_Environment_Exit,
    /* 19:262 */ ves_icall_System_Environment_FailFast_raw,
    /* 20:291 */ ves_icall_System_GC_GetMaxGeneration,
    /* 21:292 */ ves_icall_System_GC_InternalCollect,
    /* 22:293 */ ves_icall_System_GC_register_ephemeron_array_raw,
    /* 23:294 */ ves_icall_System_GC_get_ephemeron_tombstone_raw,
    /* 24:298 */ ves_icall_System_GC_WaitForPendingFinalizers,
    /* 25:299 */ ves_icall_System_GC_SuppressFinalize_raw,
    /* 26:301 */ ves_icall_System_GC_ReRegisterForFinalize_raw,
    /* 27:305 */ ves_icall_System_GC_GetGCMemoryInfo,
    /* 28:307 */ ves_icall_System_GC_AllocPinnedArray_raw,
    /* 29:312 */ ves_icall_System_Object_MemberwiseClone_raw,
    /* 30:320 */ ves_icall_System_Math_Asin,
    /* 31:321 */ ves_icall_System_Math_Atan2,
    /* 32:322 */ ves_icall_System_Math_Ceiling,
    /* 33:323 */ ves_icall_System_Math_Cos,
    /* 34:324 */ ves_icall_System_Math_Sin,
    /* 35:325 */ ves_icall_System_Math_Sqrt,
    /* 36:326 */ ves_icall_System_Math_ModF,
    /* 37:367 */ ves_icall_System_MathF_Atan2,
    /* 38:368 */ ves_icall_System_MathF_Cos,
    /* 39:369 */ ves_icall_System_MathF_Floor,
    /* 40:370 */ ves_icall_System_MathF_Pow,
    /* 41:371 */ ves_icall_System_MathF_Sin,
    /* 42:373 */ ves_icall_System_MathF_Sqrt,
    /* 43:374 */ ves_icall_System_MathF_Tan,
    /* 44:400 */ ves_icall_RuntimeMethodHandle_GetFunctionPointer_raw,
    /* 45:407 */ ves_icall_RuntimeMethodHandle_ReboxFromNullable_raw,
    /* 46:408 */ ves_icall_RuntimeMethodHandle_ReboxToNullable_raw,
    /* 47:412 */ ves_icall_RuntimeType_GetParentType_raw,
    /* 48:461 */ ves_icall_RuntimeType_GetCorrespondingInflatedMethod_raw,
    /* 49:466 */ ves_icall_RuntimeType_make_array_type_raw,
    /* 50:469 */ ves_icall_RuntimeType_make_byref_type_raw,
    /* 51:471 */ ves_icall_RuntimeType_make_pointer_type_raw,
    /* 52:476 */ ves_icall_RuntimeType_MakeGenericType_raw,
    /* 53:477 */ ves_icall_RuntimeType_GetMethodsByName_native_raw,
    /* 54:479 */ ves_icall_RuntimeType_GetPropertiesByName_native_raw,
    /* 55:480 */ ves_icall_RuntimeType_GetConstructors_native_raw,
    /* 56:484 */ ves_icall_System_RuntimeType_CreateInstanceInternal_raw,
    /* 57:485 */ ves_icall_RuntimeType_GetDeclaringMethod_raw,
    /* 58:487 */ ves_icall_System_RuntimeType_getFullName_raw,
    /* 59:488 */ ves_icall_RuntimeType_GetGenericArgumentsInternal_raw,
    /* 60:491 */ ves_icall_RuntimeType_GetGenericParameterPosition,
    /* 61:492 */ ves_icall_RuntimeType_GetEvents_native_raw,
    /* 62:493 */ ves_icall_RuntimeType_GetFields_native_raw,
    /* 63:496 */ ves_icall_RuntimeType_GetInterfaces_raw,
    /* 64:498 */ ves_icall_RuntimeType_GetDeclaringType_raw,
    /* 65:500 */ ves_icall_RuntimeType_GetName_raw,
    /* 66:502 */ ves_icall_RuntimeType_GetNamespace_raw,
    /* 67:511 */ ves_icall_RuntimeType_FunctionPointerReturnAndParameterTypes_raw,
    /* 68:563 */ ves_icall_RuntimeTypeHandle_GetAttributes,
    /* 69:565 */ ves_icall_RuntimeTypeHandle_GetMetadataToken_raw,
    /* 70:567 */ ves_icall_RuntimeTypeHandle_GetGenericTypeDefinition_impl_raw,
    /* 71:577 */ ves_icall_RuntimeTypeHandle_GetCorElementType,
    /* 72:578 */ ves_icall_RuntimeTypeHandle_HasInstantiation,
    /* 73:579 */ ves_icall_RuntimeTypeHandle_IsInstanceOfType_raw,
    /* 74:581 */ ves_icall_RuntimeTypeHandle_HasReferences_raw,
    /* 75:584 */ ves_icall_RuntimeTypeHandle_GetArrayRank_raw,
    /* 76:585 */ ves_icall_RuntimeTypeHandle_GetAssembly_raw,
    /* 77:586 */ ves_icall_RuntimeTypeHandle_GetElementType_raw,
    /* 78:587 */ ves_icall_RuntimeTypeHandle_GetModule_raw,
    /* 79:594 */ ves_icall_RuntimeTypeHandle_type_is_assignable_from_raw,
    /* 80:595 */ ves_icall_RuntimeTypeHandle_IsGenericTypeDefinition,
    /* 81:596 */ ves_icall_RuntimeTypeHandle_GetGenericParameterInfo_raw,
    /* 82:600 */ ves_icall_RuntimeTypeHandle_is_subclass_of_raw,
    /* 83:601 */ ves_icall_RuntimeTypeHandle_IsByRefLike_raw,
    /* 84:603 */ ves_icall_System_String_FastAllocateString_raw,
    /* 85:718 */ ves_icall_System_Type_internal_from_handle_raw,
    /* 86:860 */ ves_icall_System_ValueType_InternalGetHashCode_raw,
    /* 87:861 */ ves_icall_System_ValueType_Equals_raw,
    /* 88:4199 */ ves_icall_System_Threading_Interlocked_CompareExchange_Int,
    /* 89:4200 */ ves_icall_System_Threading_Interlocked_CompareExchange_Object,
    /* 90:4202 */ ves_icall_System_Threading_Interlocked_Decrement_Int,
    /* 91:4203 */ ves_icall_System_Threading_Interlocked_Increment_Int,
    /* 92:4204 */ ves_icall_System_Threading_Interlocked_Exchange_Int,
    /* 93:4205 */ ves_icall_System_Threading_Interlocked_Exchange_Object,
    /* 94:4207 */ ves_icall_System_Threading_Interlocked_CompareExchange_Long,
    /* 95:4208 */ ves_icall_System_Threading_Interlocked_Exchange_Long,
    /* 96:4209 */ ves_icall_System_Threading_Interlocked_Add_Int,
    /* 97:4221 */ ves_icall_System_Threading_Monitor_Monitor_Enter_raw,
    /* 98:4223 */ mono_monitor_exit_icall_raw,
    /* 99:4227 */ ves_icall_System_Threading_Monitor_Monitor_pulse_all_raw,
    /* 100:4229 */ ves_icall_System_Threading_Monitor_Monitor_wait_raw,
    /* 101:4231 */ ves_icall_System_Threading_Monitor_Monitor_try_enter_with_atomic_var_raw,
    /* 102:4282 */ ves_icall_System_Threading_Thread_InitInternal_raw,
    /* 103:4283 */ ves_icall_System_Threading_Thread_GetCurrentThread,
    /* 104:4285 */ ves_icall_System_Threading_InternalThread_Thread_free_internal_raw,
    /* 105:4286 */ ves_icall_System_Threading_Thread_GetState_raw,
    /* 106:4287 */ ves_icall_System_Threading_Thread_SetState_raw,
    /* 107:4288 */ ves_icall_System_Threading_Thread_ClrState_raw,
    /* 108:4289 */ ves_icall_System_Threading_Thread_SetName_icall_raw,
    /* 109:4291 */ ves_icall_System_Threading_Thread_YieldInternal,
    /* 110:4293 */ ves_icall_System_Threading_Thread_SetPriority_raw,
    /* 111:4770 */ ves_icall_System_Runtime_Loader_AssemblyLoadContext_PrepareForAssemblyLoadContextRelease_raw,
    /* 112:4773 */ ves_icall_System_Runtime_Loader_AssemblyLoadContext_GetLoadContextForAssembly_raw,
    /* 113:4775 */ ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFile_raw,
    /* 114:4776 */ ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalInitializeNativeALC_raw,
    /* 115:4777 */ ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFromStream_raw,
    /* 116:4914 */ ves_icall_System_GCHandle_InternalAlloc_raw,
    /* 117:4915 */ ves_icall_System_GCHandle_InternalFree_raw,
    /* 118:4916 */ ves_icall_System_GCHandle_InternalGet_raw,
    /* 119:4936 */ ves_icall_System_Runtime_InteropServices_Marshal_GetLastPInvokeError,
    /* 120:4937 */ ves_icall_System_Runtime_InteropServices_Marshal_SetLastPInvokeError,
    /* 121:4938 */ ves_icall_System_Runtime_InteropServices_Marshal_StructureToPtr_raw,
    /* 122:4940 */ ves_icall_System_Runtime_InteropServices_Marshal_SizeOfHelper_raw,
    /* 123:5059 */ ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalGetHashCode_raw,
    /* 124:5069 */ ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetUninitializedObjectInternal_raw,
    /* 125:5070 */ ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray_raw,
    /* 126:5071 */ ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetSpanDataFrom_raw,
    /* 127:5072 */ ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_SufficientExecutionStack,
    /* 128:5073 */ ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalBox_raw,
    /* 129:5199 */ ves_icall_System_Reflection_Assembly_GetEntryAssembly_raw,
    /* 130:5201 */ ves_icall_System_Reflection_Assembly_InternalLoad_raw,
    /* 131:5221 */ ves_icall_System_Reflection_AssemblyName_GetNativeName,
    /* 132:5235 */ ves_icall_MonoCustomAttrs_GetCustomAttributesInternal_raw,
    /* 133:5241 */ ves_icall_MonoCustomAttrs_GetCustomAttributesDataInternal_raw,
    /* 134:5248 */ ves_icall_MonoCustomAttrs_IsDefinedInternal_raw,
    /* 135:5259 */ ves_icall_System_Reflection_FieldInfo_internal_from_handle_type_raw,
    /* 136:5262 */ ves_icall_System_Reflection_FieldInfo_get_marshal_info_raw,
    /* 137:5278 */ ves_icall_System_Reflection_LoaderAllocatorScout_Destroy,
    /* 138:5349 */ ves_icall_System_Reflection_RuntimeAssembly_GetInfo_raw,
    /* 139:5351 */ ves_icall_System_Reflection_Assembly_GetManifestModuleInternal_raw,
    /* 140:5357 */ ves_icall_System_Reflection_RuntimeCustomAttributeData_ResolveArgumentsInternal_raw,
    /* 141:5365 */ ves_icall_RuntimeEventInfo_get_event_info_raw,
    /* 142:5383 */ ves_icall_reflection_get_token_raw,
    /* 143:5384 */ ves_icall_System_Reflection_EventInfo_internal_from_handle_type_raw,
    /* 144:5392 */ ves_icall_RuntimeFieldInfo_ResolveType_raw,
    /* 145:5394 */ ves_icall_RuntimeFieldInfo_GetParentType_raw,
    /* 146:5400 */ ves_icall_RuntimeFieldInfo_GetFieldOffset_raw,
    /* 147:5401 */ ves_icall_RuntimeFieldInfo_GetValueInternal_raw,
    /* 148:5404 */ ves_icall_RuntimeFieldInfo_GetRawConstantValue_raw,
    /* 149:5408 */ ves_icall_reflection_get_token_raw,
    /* 150:5414 */ ves_icall_get_method_info_raw,
    /* 151:5415 */ ves_icall_get_method_attributes,
    /* 152:5422 */ ves_icall_System_Reflection_MonoMethodInfo_get_parameter_info_raw,
    /* 153:5424 */ ves_icall_System_MonoMethodInfo_get_retval_marshal_raw,
    /* 154:5435 */ ves_icall_System_Reflection_RuntimeMethodInfo_GetMethodFromHandleInternalType_native_raw,
    /* 155:5438 */ ves_icall_RuntimeMethodInfo_get_name_raw,
    /* 156:5439 */ ves_icall_RuntimeMethodInfo_get_base_method_raw,
    /* 157:5440 */ ves_icall_reflection_get_token_raw,
    /* 158:5450 */ ves_icall_InternalInvoke_raw,
    /* 159:5459 */ ves_icall_RuntimeMethodInfo_GetPInvoke_raw,
    /* 160:5464 */ ves_icall_RuntimeMethodInfo_GetGenericArguments_raw,
    /* 161:5465 */ ves_icall_RuntimeMethodInfo_get_IsGenericMethodDefinition_raw,
    /* 162:5466 */ ves_icall_RuntimeMethodInfo_get_IsGenericMethod_raw,
    /* 163:5482 */ ves_icall_InvokeClassConstructor_raw,
    /* 164:5484 */ ves_icall_InternalInvoke_raw,
    /* 165:5497 */ ves_icall_reflection_get_token_raw,
    /* 166:5531 */ ves_icall_RuntimePropertyInfo_get_property_info_raw,
    /* 167:5553 */ ves_icall_reflection_get_token_raw,
    /* 168:5554 */ ves_icall_System_Reflection_RuntimePropertyInfo_internal_from_handle_type_raw,
    /* 169:5937 */ ves_icall_DynamicMethod_create_dynamic_method_raw,
    /* 170:5991 */ ves_icall_AssemblyBuilder_basic_init_raw,
    /* 171:5992 */ ves_icall_AssemblyBuilder_UpdateNativeCustomAttributes_raw,
    /* 172:6121 */ ves_icall_ModuleBuilder_basic_init_raw,
    /* 173:6122 */ ves_icall_ModuleBuilder_set_wrappers_type_raw,
    /* 174:6126 */ ves_icall_ModuleBuilder_getToken_raw,
    /* 175:6129 */ ves_icall_ModuleBuilder_RegisterToken_raw,
    /* 176:6168 */ ves_icall_TypeBuilder_create_runtime_class_raw,
    /* 177:6653 */ ves_icall_System_Diagnostics_Debugger_IsAttached_internal,
    /* 178:6657 */ ves_icall_System_Diagnostics_StackFrame_GetFrameInfo,
    /* 179:6667 */ ves_icall_System_Diagnostics_StackTrace_GetTrace,
    /* 180:6712 */ ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogWaitHandleWaitStart,
    /* 181:6713 */ ves_icall_System_Diagnostics_Tracing_NativeRuntimeEventSource_LogWaitHandleWaitStop,
    /* 182:7047 */ ves_icall_Mono_RuntimeClassHandle_GetTypeFromClass,
    /* 183:7068 */ ves_icall_Mono_RuntimeGPtrArrayHandle_GPtrArrayFree,
    /* 184:7070 */ ves_icall_Mono_SafeStringMarshal_StringToUtf8,
    /* 185:7072 */ ves_icall_Mono_SafeStringMarshal_GFree
};

static uint8_t corlib_icall_flags [] = {
    /* 0:131 */ 0,
    /* 1:138 */ 0,
    /* 2:139 */ 0,
    /* 3:140 */ 0,
    /* 4:141 */ 4,
    /* 5:142 */ 4,
    /* 6:143 */ 0,
    /* 7:144 */ 4,
    /* 8:146 */ 4,
    /* 9:172 */ 0,
    /* 10:173 */ 0,
    /* 11:174 */ 0,
    /* 12:192 */ 4,
    /* 13:193 */ 4,
    /* 14:196 */ 4,
    /* 15:197 */ 0,
    /* 16:198 */ 4,
    /* 17:258 */ 0,
    /* 18:259 */ 0,
    /* 19:262 */ 4,
    /* 20:291 */ 0,
    /* 21:292 */ 0,
    /* 22:293 */ 4,
    /* 23:294 */ 4,
    /* 24:298 */ 0,
    /* 25:299 */ 4,
    /* 26:301 */ 4,
    /* 27:305 */ 0,
    /* 28:307 */ 4,
    /* 29:312 */ 4,
    /* 30:320 */ 0,
    /* 31:321 */ 0,
    /* 32:322 */ 0,
    /* 33:323 */ 0,
    /* 34:324 */ 0,
    /* 35:325 */ 0,
    /* 36:326 */ 0,
    /* 37:367 */ 0,
    /* 38:368 */ 0,
    /* 39:369 */ 0,
    /* 40:370 */ 0,
    /* 41:371 */ 0,
    /* 42:373 */ 0,
    /* 43:374 */ 0,
    /* 44:400 */ 4,
    /* 45:407 */ 4,
    /* 46:408 */ 4,
    /* 47:412 */ 4,
    /* 48:461 */ 4,
    /* 49:466 */ 4,
    /* 50:469 */ 4,
    /* 51:471 */ 4,
    /* 52:476 */ 4,
    /* 53:477 */ 4,
    /* 54:479 */ 4,
    /* 55:480 */ 4,
    /* 56:484 */ 4,
    /* 57:485 */ 4,
    /* 58:487 */ 4,
    /* 59:488 */ 4,
    /* 60:491 */ 0,
    /* 61:492 */ 4,
    /* 62:493 */ 4,
    /* 63:496 */ 4,
    /* 64:498 */ 4,
    /* 65:500 */ 4,
    /* 66:502 */ 4,
    /* 67:511 */ 4,
    /* 68:563 */ 0,
    /* 69:565 */ 4,
    /* 70:567 */ 4,
    /* 71:577 */ 0,
    /* 72:578 */ 0,
    /* 73:579 */ 4,
    /* 74:581 */ 4,
    /* 75:584 */ 4,
    /* 76:585 */ 4,
    /* 77:586 */ 4,
    /* 78:587 */ 4,
    /* 79:594 */ 4,
    /* 80:595 */ 0,
    /* 81:596 */ 4,
    /* 82:600 */ 4,
    /* 83:601 */ 4,
    /* 84:603 */ 4,
    /* 85:718 */ 4,
    /* 86:860 */ 4,
    /* 87:861 */ 4,
    /* 88:4199 */ 0,
    /* 89:4200 */ 0,
    /* 90:4202 */ 0,
    /* 91:4203 */ 0,
    /* 92:4204 */ 0,
    /* 93:4205 */ 0,
    /* 94:4207 */ 0,
    /* 95:4208 */ 0,
    /* 96:4209 */ 0,
    /* 97:4221 */ 4,
    /* 98:4223 */ 4,
    /* 99:4227 */ 4,
    /* 100:4229 */ 4,
    /* 101:4231 */ 4,
    /* 102:4282 */ 4,
    /* 103:4283 */ 0,
    /* 104:4285 */ 4,
    /* 105:4286 */ 4,
    /* 106:4287 */ 4,
    /* 107:4288 */ 4,
    /* 108:4289 */ 4,
    /* 109:4291 */ 0,
    /* 110:4293 */ 4,
    /* 111:4770 */ 4,
    /* 112:4773 */ 4,
    /* 113:4775 */ 4,
    /* 114:4776 */ 4,
    /* 115:4777 */ 4,
    /* 116:4914 */ 4,
    /* 117:4915 */ 4,
    /* 118:4916 */ 4,
    /* 119:4936 */ 0,
    /* 120:4937 */ 0,
    /* 121:4938 */ 4,
    /* 122:4940 */ 4,
    /* 123:5059 */ 4,
    /* 124:5069 */ 4,
    /* 125:5070 */ 4,
    /* 126:5071 */ 4,
    /* 127:5072 */ 0,
    /* 128:5073 */ 4,
    /* 129:5199 */ 4,
    /* 130:5201 */ 4,
    /* 131:5221 */ 0,
    /* 132:5235 */ 4,
    /* 133:5241 */ 4,
    /* 134:5248 */ 4,
    /* 135:5259 */ 4,
    /* 136:5262 */ 4,
    /* 137:5278 */ 0,
    /* 138:5349 */ 4,
    /* 139:5351 */ 4,
    /* 140:5357 */ 4,
    /* 141:5365 */ 4,
    /* 142:5383 */ 4,
    /* 143:5384 */ 4,
    /* 144:5392 */ 4,
    /* 145:5394 */ 4,
    /* 146:5400 */ 4,
    /* 147:5401 */ 4,
    /* 148:5404 */ 4,
    /* 149:5408 */ 4,
    /* 150:5414 */ 4,
    /* 151:5415 */ 0,
    /* 152:5422 */ 4,
    /* 153:5424 */ 4,
    /* 154:5435 */ 4,
    /* 155:5438 */ 4,
    /* 156:5439 */ 4,
    /* 157:5440 */ 4,
    /* 158:5450 */ 4,
    /* 159:5459 */ 4,
    /* 160:5464 */ 4,
    /* 161:5465 */ 4,
    /* 162:5466 */ 4,
    /* 163:5482 */ 4,
    /* 164:5484 */ 4,
    /* 165:5497 */ 4,
    /* 166:5531 */ 4,
    /* 167:5553 */ 4,
    /* 168:5554 */ 4,
    /* 169:5937 */ 4,
    /* 170:5991 */ 4,
    /* 171:5992 */ 4,
    /* 172:6121 */ 4,
    /* 173:6122 */ 4,
    /* 174:6126 */ 4,
    /* 175:6129 */ 4,
    /* 176:6168 */ 4,
    /* 177:6653 */ 0,
    /* 178:6657 */ 0,
    /* 179:6667 */ 0,
    /* 180:6712 */ 0,
    /* 181:6713 */ 0,
    /* 182:7047 */ 0,
    /* 183:7068 */ 0,
    /* 184:7070 */ 0,
    /* 185:7072 */ 0
};
