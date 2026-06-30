/**
 * SCS SDK - Core types
 * Based on SCS Software's public Telemetry SDK
 */
#pragma once
#include <stdint.h>

#define SCSSDK_RESULT_ok                          0
#define SCSSDK_RESULT_unsupported_version        -1
#define SCSSDK_RESULT_already_registered         -2
#define SCSSDK_RESULT_generic_error              -3

typedef int32_t  scs_result_t;
typedef uint32_t scs_u32_t;
typedef int32_t  scs_s32_t;
typedef uint64_t scs_u64_t;
typedef float    scs_float_t;
typedef double   scs_double_t;
typedef uint8_t  scs_u8_t;

typedef const char* scs_string_t;

typedef struct {
    scs_float_t x, y, z;
} scs_value_fvector_t;

typedef struct {
    scs_double_t x, y, z;
} scs_value_dvector_t;

typedef struct {
    scs_float_t heading;
    scs_float_t pitch;
    scs_float_t roll;
} scs_value_euler_t;

#define SCS_VALUE_TYPE_INVALID       0
#define SCS_VALUE_TYPE_bool          1
#define SCS_VALUE_TYPE_s32           2
#define SCS_VALUE_TYPE_u32           3
#define SCS_VALUE_TYPE_u64           4
#define SCS_VALUE_TYPE_float         5
#define SCS_VALUE_TYPE_double        6
#define SCS_VALUE_TYPE_fvector       7
#define SCS_VALUE_TYPE_dvector       8
#define SCS_VALUE_TYPE_euler         9
#define SCS_VALUE_TYPE_fplacement   10
#define SCS_VALUE_TYPE_dplacement   11
#define SCS_VALUE_TYPE_string       12
#define SCS_VALUE_TYPE_s64          13

typedef uint32_t scs_value_type_t;

typedef struct {
    uint32_t boolean;
} scs_value_bool_t;

typedef struct {
    scs_value_type_t type;
    uint32_t         _padding;
    union {
        scs_value_bool_t    value_bool;
        scs_s32_t           value_s32;
        scs_u32_t           value_u32;
        scs_u64_t           value_u64;
        scs_float_t         value_float;
        scs_double_t        value_double;
        scs_value_fvector_t value_fvector;
        scs_value_dvector_t value_dvector;
        scs_value_euler_t   value_euler;
        scs_string_t        value_string;
    };
} scs_value_t;

typedef struct {
    uint32_t major;
    uint32_t minor;
} scs_sdk_version_t;

typedef void (*scs_log_t)(const scs_u32_t type, const scs_string_t message);

#define SCS_LOG_TYPE_message 0
#define SCS_LOG_TYPE_warning 1
#define SCS_LOG_TYPE_error   2

typedef struct {
    scs_u32_t version;
    scs_log_t log;
} scs_sdk_init_params_t;
