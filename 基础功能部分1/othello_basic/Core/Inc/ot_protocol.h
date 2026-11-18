#ifndef OT_PROTOCOL_H
#define OT_PROTOCOL_H
#include "othello.h"
#include <stddef.h>
#define OT_LINE_MAX 160u
/* Input whole LF-terminated or LF-stripped line. Output buffer >= OT_LINE_MAX. */
uint16_t ot_crc(const char *s,size_t n);
int ot_state(const OtGame *g,uint32_t seq,char *out,size_t cap);
int ot_command(OtGame *g,const char *line,char *out,size_t cap);
#endif
