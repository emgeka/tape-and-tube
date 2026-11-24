/* SPDX-License-Identifier: GPL-3.0-only
 * Tape & Tube experimental host API 1; NTSCRT's codec retains its MIT license.
 * All pointers must be valid for their stated lengths during the call.
 * Functions return NULL on success, otherwise an owned UTF-8 error string.
 * Free returned strings exactly once using tt_digital_string_free().
 * Calls never unwind across this boundary. Foreign callbacks must not unwind.
 */
#ifndef TAPETUBE_DIGITAL_H
#define TAPETUBE_DIGITAL_H
#include <stddef.h>
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
enum { TT_DIGITAL_RGBA8 = 1, TT_DIGITAL_STILL = 1 };
typedef struct {
    uint32_t struct_size, format, width, height;
    size_t stride;
    uint32_t capture_width, capture_height;
    int64_t frame_index;
    uint32_t fps_num, fps_den, flags, reserved;
} tt_digital_frame_context;
typedef struct {
    uint32_t struct_size, format, width, height;
    size_t stride;
    const uint8_t *pixels;
    size_t length;
} tt_digital_frame_view;
/* A provider synchronously calls sink exactly once and returns 1 on success.
 * Frame storage only needs to survive the sink call: the host copies it.
 * Return 0 when missing, never substitute the current frame for video history.
 * sink returns 1 if accepted, 0 if invalid. No retained pointers/callbacks.
 * The requested index is a SOURCE frame index, not a camera capture index.
 */
typedef int32_t (*tt_digital_sink)(void *, const tt_digital_frame_view *);
typedef int32_t (*tt_digital_provider)(void *, int64_t, tt_digital_sink, void *);
uint32_t tt_digital_host_api_version(void);
char *tt_digital_metadata_json(void);
char *tt_digital_defaults_json(void);
char *tt_digital_create(const uint8_t *json, size_t length, uint64_t *out_handle);
char *tt_digital_process(uint64_t handle, const tt_digital_frame_context *context,
                         uint8_t *pixels, size_t length,
                         tt_digital_provider provider, void *user);
char *tt_digital_reset(uint64_t handle);
char *tt_digital_destroy(uint64_t handle);
void tt_digital_string_free(char *value);
#ifdef __cplusplus
}
#endif
#endif
