/**
 * ETS2 Autopilot Telemetry Plugin
 * Reads game state and writes to shared memory for the Autopilot app.
 */
#include <windows.h>
#include <cstdio>
#include <cstring>
#include <cstdint>
#include "../include/scssdk.h"
#include "../include/scssdk_telemetry.h"

// Shared memory layout - must match C# struct exactly
#pragma pack(push, 1)
struct TelemetryData {
    uint32_t magic;           // 0x45545332 = "ETS2"
    uint32_t version;         // struct version

    // Truck position & orientation
    double   worldX;
    double   worldY;
    double   worldZ;
    float    headingDeg;      // 0-360 degrees
    float    pitchDeg;
    float    rollDeg;

    // Speed & engine
    float    speedKmh;
    float    throttle;        // 0.0 - 1.0
    float    brake;           // 0.0 - 1.0
    float    steering;        // -1.0 to 1.0
    int32_t  gear;
    float    rpmPercent;

    // Navigation
    float    navDistanceM;    // meters to next turn
    float    navSpeedLimitMs; // speed limit m/s
    float    navTimeRemaining;

    // Game state
    uint8_t  gamePaused;
    uint8_t  cruiseControlOn;
    uint8_t  parkBrakeOn;
    uint8_t  _pad;

    uint64_t timestamp;       // milliseconds
};
#pragma pack(pop)

static const char*    SHARED_MEMORY_NAME = "ETS2AutopilotTelemetry";
static const uint32_t SHARED_MEMORY_SIZE = sizeof(TelemetryData);
static const uint32_t MAGIC              = 0x45545332;

static HANDLE          g_hMapFile = NULL;
static TelemetryData*  g_pData    = NULL;
static scs_log_t       g_log      = nullptr;
static bool            g_paused   = true;

static void log_msg(const char* msg) {
    if (g_log) g_log(SCS_LOG_TYPE_message, msg);
}

static void update_timestamp() {
    if (!g_pData) return;
    FILETIME ft;
    GetSystemTimeAsFileTime(&ft);
    ULARGE_INTEGER li;
    li.LowPart  = ft.dwLowDateTime;
    li.HighPart = ft.dwHighDateTime;
    // Convert from 100ns intervals since 1601 to ms since epoch
    g_pData->timestamp = (li.QuadPart - 116444736000000000ULL) / 10000ULL;
}

// --- Telemetry callbacks ---

static void SCSAPI_CALL cb_speed(const scs_string_t, const scs_u32_t,
                                  const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->speedKmh = val->value_float * 3.6f; // m/s -> km/h
}

static void SCSAPI_CALL cb_placement(const scs_string_t, const scs_u32_t,
                                      const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    // dplacement: position + orientation
    // The value is accessed via value_dvector for position
    // and value_euler for orientation via a placement struct
    // We store heading from the euler angles
}

static void SCSAPI_CALL cb_steering(const scs_string_t, const scs_u32_t,
                                     const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->steering = val->value_float;
}

static void SCSAPI_CALL cb_throttle(const scs_string_t, const scs_u32_t,
                                     const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->throttle = val->value_float;
}

static void SCSAPI_CALL cb_brake(const scs_string_t, const scs_u32_t,
                                  const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->brake = val->value_float;
}

static void SCSAPI_CALL cb_gear(const scs_string_t, const scs_u32_t,
                                 const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->gear = val->value_s32;
}

static void SCSAPI_CALL cb_nav_distance(const scs_string_t, const scs_u32_t,
                                         const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->navDistanceM = val->value_float;
}

static void SCSAPI_CALL cb_nav_speed_limit(const scs_string_t, const scs_u32_t,
                                            const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->navSpeedLimitMs = val->value_float;
}

static void SCSAPI_CALL cb_cruise_control(const scs_string_t, const scs_u32_t,
                                           const scs_value_t* val, const scs_uintptr_t) {
    if (!g_pData || !val) return;
    g_pData->cruiseControlOn = (uint8_t)val->value_bool.boolean;
}

// --- Frame event (called every game frame) ---
static void SCSAPI_CALL cb_frame_start(const scs_u32_t, const void*,
                                        const scs_uintptr_t) {
    if (!g_pData || g_paused) return;
    update_timestamp();
}

static void SCSAPI_CALL cb_paused(const scs_u32_t event, const void*,
                                   const scs_uintptr_t) {
    g_paused = (event == SCS_TELEMETRY_EVENT_paused);
    if (g_pData) g_pData->gamePaused = g_paused ? 1 : 0;
}

// --- Plugin entry points ---

SCSAPI_RESULT scs_telemetry_init(const scs_u32_t version,
                                  const scs_telemetry_init_params_t* const params) {
    if (version != SCS_TELEMETRY_VERSION_CURRENT) {
        return SCS_RESULT_unsupported_version;
    }

    g_log = params->log;
    log_msg("[ETS2Autopilot] Plugin initializing...");

    // Create shared memory
    g_hMapFile = CreateFileMappingA(
        INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE,
        0, SHARED_MEMORY_SIZE, SHARED_MEMORY_NAME
    );

    if (!g_hMapFile) {
        log_msg("[ETS2Autopilot] ERROR: Failed to create shared memory");
        return SCS_RESULT_generic_error;
    }

    g_pData = (TelemetryData*)MapViewOfFile(
        g_hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, SHARED_MEMORY_SIZE
    );

    if (!g_pData) {
        log_msg("[ETS2Autopilot] ERROR: Failed to map shared memory");
        CloseHandle(g_hMapFile);
        g_hMapFile = NULL;
        return SCS_RESULT_generic_error;
    }

    memset(g_pData, 0, SHARED_MEMORY_SIZE);
    g_pData->magic   = MAGIC;
    g_pData->version = 1;

    // Register events
    params->register_for_event(SCS_TELEMETRY_EVENT_paused,      cb_paused,      0);
    params->register_for_event(SCS_TELEMETRY_EVENT_started,     cb_paused,      0);
    params->register_for_event(SCS_TELEMETRY_EVENT_frame_start, cb_frame_start, 0);

    // Register channels
    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_speed,
        SCS_U32_NIL, SCS_VALUE_TYPE_float, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_speed, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_steering_input,
        SCS_U32_NIL, SCS_VALUE_TYPE_float, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_steering, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_throttle_input,
        SCS_U32_NIL, SCS_VALUE_TYPE_float, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_throttle, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_brake_input,
        SCS_U32_NIL, SCS_VALUE_TYPE_float, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_brake, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_engine_gear,
        SCS_U32_NIL, SCS_VALUE_TYPE_s32, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_gear, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_navigation_distance,
        SCS_U32_NIL, SCS_VALUE_TYPE_float, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_nav_distance, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_navigation_speed_limit,
        SCS_U32_NIL, SCS_VALUE_TYPE_float, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_nav_speed_limit, 0);

    params->register_for_channel(SCS_TELEMETRY_TRUCK_CHANNEL_cruise_control,
        SCS_U32_NIL, SCS_VALUE_TYPE_bool, SCS_TELEMETRY_CHANNEL_FLAG_none,
        cb_cruise_control, 0);

    log_msg("[ETS2Autopilot] Plugin initialized. Shared memory: " SHARED_MEMORY_NAME);
    return SCS_RESULT_ok;
}

SCSAPI_VOID scs_telemetry_shutdown(void) {
    if (g_pData)   { UnmapViewOfFile(g_pData); g_pData = NULL; }
    if (g_hMapFile){ CloseHandle(g_hMapFile);  g_hMapFile = NULL; }
    g_log = nullptr;
}
