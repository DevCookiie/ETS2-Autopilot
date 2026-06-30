cmake_minimum_required(VERSION 3.20)
project(ets2autopilot_plugin)

set(CMAKE_CXX_STANDARD 17)

add_library(ets2autopilot SHARED
    src/autopilot_plugin.cpp
)

target_include_directories(ets2autopilot PRIVATE include)

# Plugin must be a DLL without lib prefix
set_target_properties(ets2autopilot PROPERTIES
    PREFIX ""
    OUTPUT_NAME "ets2autopilot"
)

# Link Windows libs
target_link_libraries(ets2autopilot PRIVATE kernel32)

install(TARGETS ets2autopilot
    RUNTIME DESTINATION .
)
