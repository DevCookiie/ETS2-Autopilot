/**
 * SCS Telemetry SDK - Telemetry channels
 */
#pragma once
#include "scssdk.h"

#define SCS_TELEMETRY_VERSION_1_00     0x00010000
#define SCS_TELEMETRY_VERSION_CURRENT  SCS_TELEMETRY_VERSION_1_00

// Channel registration flags
#define SCS_TELEMETRY_CHANNEL_FLAG_none              0x00000000
#define SCS_TELEMETRY_CHANNEL_FLAG_no_value          0x00000002

typedef void (*scs_telemetry_channel_callback_t)(
    const scs_string_t    name,
    const scs_u32_t       index,
    const scs_value_t*    value,
    const scs_uintptr_t   context
);

typedef scs_result_t (*scs_telemetry_register_for_channel_t)(
    const scs_string_t                      name,
    const scs_u32_t                         index,
    const scs_value_type_t                  type,
    const scs_u32_t                         flags,
    const scs_telemetry_channel_callback_t  callback,
    const scs_uintptr_t                     context
);

typedef scs_result_t (*scs_telemetry_unregister_from_channel_t)(
    const scs_string_t  name,
    const scs_u32_t     index,
    const scs_value_type_t type
);

typedef void (*scs_telemetry_event_callback_t)(
    const scs_u32_t     event,
    const void*         event_info,
    const scs_uintptr_t context
);

typedef scs_result_t (*scs_telemetry_register_for_event_t)(
    const scs_u32_t                     event,
    const scs_telemetry_event_callback_t callback,
    const scs_uintptr_t                 context
);

typedef scs_result_t (*scs_telemetry_unregister_from_event_t)(
    const scs_u32_t event
);

#define SCS_TELEMETRY_EVENT_invalid        0
#define SCS_TELEMETRY_EVENT_frame_start    1
#define SCS_TELEMETRY_EVENT_frame_end      2
#define SCS_TELEMETRY_EVENT_paused         3
#define SCS_TELEMETRY_EVENT_started        4

typedef uintptr_t scs_uintptr_t;

typedef struct {
    scs_u32_t                                version;
    scs_log_t                                log;
    scs_telemetry_register_for_channel_t     register_for_channel;
    scs_telemetry_unregister_from_channel_t  unregister_from_channel;
    scs_telemetry_register_for_event_t       register_for_event;
    scs_telemetry_unregister_from_event_t    unregister_from_event;
} scs_telemetry_init_params_t;

// Truck telemetry channels
#define SCS_TELEMETRY_TRUCK_CHANNEL_world_placement         "truck.world.placement"
#define SCS_TELEMETRY_TRUCK_CHANNEL_speed                   "truck.speed"
#define SCS_TELEMETRY_TRUCK_CHANNEL_engine_rpm              "truck.engine.rpm"
#define SCS_TELEMETRY_TRUCK_CHANNEL_engine_gear             "truck.engine.gear"
#define SCS_TELEMETRY_TRUCK_CHANNEL_steering_input          "truck.steer"
#define SCS_TELEMETRY_TRUCK_CHANNEL_throttle_input          "truck.throttle"
#define SCS_TELEMETRY_TRUCK_CHANNEL_brake_input             "truck.brake"
#define SCS_TELEMETRY_TRUCK_CHANNEL_cruise_control_speed    "truck.cruise_control.speed"
#define SCS_TELEMETRY_TRUCK_CHANNEL_cruise_control          "truck.cruise_control"

// Navigation channels
#define SCS_TELEMETRY_TRUCK_CHANNEL_navigation_distance     "truck.navigation.distance"
#define SCS_TELEMETRY_TRUCK_CHANNEL_navigation_time         "truck.navigation.time"
#define SCS_TELEMETRY_TRUCK_CHANNEL_navigation_speed_limit  "truck.navigation.speed.limit"

typedef scs_result_t __stdcall scs_telemetry_init_t(
    const scs_u32_t                 version,
    const scs_telemetry_init_params_t* params
);

typedef void __stdcall scs_telemetry_shutdown_t(void);
