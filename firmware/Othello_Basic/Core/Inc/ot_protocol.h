#ifndef OT_PROTOCOL_H
#define OT_PROTOCOL_H
#include "ot_match.h"
#include <stddef.h>
#define OT_LINE_MAX 192u
/* Input whole LF-terminated or LF-stripped line. Output buffer >= OT_LINE_MAX. */
uint16_t ot_crc(const char *s,size_t n);
int ot_state(const OtGame *g,uint32_t seq,char *out,size_t cap);
int ot_game_state(const OtMatch *match, uint32_t seq, char *out, size_t cap);
int ot_match_command(OtMatch *match, const char *line, char *out, size_t cap);
#endif
