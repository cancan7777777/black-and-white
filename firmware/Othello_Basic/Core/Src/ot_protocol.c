#include "ot_protocol.h"
#include <stdio.h>
#include <string.h>
uint16_t ot_crc(const char *s,size_t n){uint16_t c=0xffff;size_t i;unsigned b;for(i=0;i<n;i++){c^=(uint16_t)((uint8_t)s[i]<<8);for(b=0;b<8;b++)c=(uint16_t)((c&0x8000)?((uint32_t)c<<1)^0x1021u:((uint32_t)c<<1));}return c;}
static int finish(char *out,size_t cap,int n){int k;if(n<0||(size_t)n>=cap)return -1;k=snprintf(out+n,cap-(size_t)n,",%04X\n",(unsigned)ot_crc(out,(size_t)n));return k==6 && (size_t)n+6<cap ? n+k:-1;}

static int state_payload(const OtGame *g, const char *tag, uint32_t seq, char *out, size_t cap)
{
    char board[65];
    unsigned i;
    for (i = 0; i < 64u; ++i) board[i] = (char)('0' + g->board[i]);
    board[64] = 0;
    return snprintf(out, cap, "%s,%lu,%lu,%u,%u,%u,%u,%s", tag,
                    (unsigned long)seq, (unsigned long)g->revision, g->turn,
                    g->status, g->black, g->white, board);
}

int ot_state(const OtGame *g, uint32_t seq, char *out, size_t cap)
{
    return finish(out, cap, state_payload(g, "@STATE", seq, out, cap));
}

int ot_match_state(const OtMatch *m, OtStateFormat format, uint32_t seq, char *out, size_t cap)
{
    static const char *const tags[] = { "@STATE", "@GAME", "@GAME2", "@GAME3", "@GAME4", "@GAME5", "@GAME6" };
    OtMode wire_mode = m->mode == OT_EXTERNAL_AI && format < OT_FORMAT_EXTERNAL_AI ? OT_CASUAL : m->mode;
    uint32_t remaining = m->result == OT_RESULT_TIME ? 0u : m->remaining_ms[m->game.turn == OT_WHITE ? 1u : 0u];
    int base, n;
    if (format == OT_FORMAT_BASIC) return ot_state(&m->game, seq, out, cap);
    base = state_payload(&m->game, tags[format], seq, out, cap);
    if (base < 0 || (size_t)base >= cap) return -1;
    n = snprintf(out + base, cap - (size_t)base, ",%u,%u,%lu,%lu,%u,%lu,%lu,%u,%u,%u",
                 (unsigned)wire_mode, (unsigned)m->phase, (unsigned long)m->round,
                 (unsigned long)m->total, m->losses, (unsigned long)remaining,
                 (unsigned long)m->elapsed_ms, m->edited, (unsigned)m->result, m->limit_seconds);
    if (n < 0 || (size_t)n >= cap - (size_t)base) return -1;
    base += n;
    if (format >= OT_FORMAT_STAGES) {
        n = snprintf(out + base, cap - (size_t)base, ",%u,%u,%u",
                     (unsigned)m->stage, ot_match_score_half(m), (unsigned)m->total_half_point);
        if (n < 0 || (size_t)n >= cap - (size_t)base) return -1;
        base += n;
    }
    if (format == OT_FORMAT_CLOCKS) {
        n = snprintf(out + base, cap - (size_t)base, ",%lu,%lu",
                     (unsigned long)m->remaining_ms[0], (unsigned long)m->remaining_ms[1]);
        if (n < 0 || (size_t)n >= cap - (size_t)base) return -1;
        base += n;
    }
    return finish(out, cap, base);
}
static int err(uint32_t seq,const char *why,char *out,size_t cap){return finish(out,cap,snprintf(out,cap,"@ERROR,%lu,%s",(unsigned long)seq,why));}
static int number(const char *s,uint32_t *v){uint32_t n=0;unsigned d;if(!s||!*s)return 0;while(*s){if(*s<'0'||*s>'9')return 0;d=(unsigned)(*s++-'0');if(n>(UINT32_MAX-d)/10u)return 0;n=n*10u+d;}*v=n;return 1;}
static int hex4(const char *s,uint16_t *v){unsigned i,d;uint16_t n=0;if(strlen(s)!=4)return 0;for(i=0;i<4;i++){char c=s[i];if(c>='0'&&c<='9')d=c-'0';else if(c>='A'&&c<='F')d=c-'A'+10;else if(c>='a'&&c<='f')d=c-'a'+10;else return 0;n=(uint16_t)((n<<4)|d);}*v=n;return 1;}
int ot_match_command(OtMatch *m, OtStateFormat *format, const char *line, char *out, size_t cap)
{
    static const char *const names[] = { "START", "PAUSE", "RESUME", "END", "RESTART", "MODE", "EDIT" };
    char buf[OT_LINE_MAX], *last, *fields[6], *p;
    size_t n = strlen(line);
    unsigned count = 1, action;
    uint16_t crc;
    uint32_t seq = 0, revision, a, b, piece;
    uint8_t legacy, move, ai_move;
    if (n >= sizeof(buf)) return err(0, "LENGTH", out, cap);
    memcpy(buf, line, n + 1u);
    while (n && (buf[n-1] == '\n' || buf[n-1] == '\r')) buf[--n] = 0;
    last = strrchr(buf, ',');
    if (!last || !hex4(last + 1, &crc) || ot_crc(buf, (size_t)(last - buf)) != crc)
        return err(0, "CRC", out, cap);
    *last = 0;
    fields[0] = buf;
    for (p = buf; *p; ++p) if (*p == ',') {
        if (count >= 6u) return err(0, "FORMAT", out, cap);
        *p = 0;
        fields[count++] = p + 1;
    }
    if (count < 2u || !number(fields[1], &seq) || !seq) return err(0, "FORMAT", out, cap);
    if (count == 2u) {
        OtStateFormat reply;
        if (!strcmp(buf, "@GET")) reply = OT_FORMAT_BASIC;
        else if (!strcmp(buf, "@SYNC")) reply = OT_FORMAT_ADVANCED;
        else if (!strcmp(buf, "@SYNC2")) reply = OT_FORMAT_COMPUTER;
        else if (!strcmp(buf, "@SYNC3")) reply = OT_FORMAT_STAGES;
        else if (!strcmp(buf, "@SYNC4")) reply = OT_FORMAT_KEYBOARD_AI;
        else if (!strcmp(buf, "@SYNC5")) reply = OT_FORMAT_EXTERNAL_AI;
        else if (!strcmp(buf, "@SYNC6")) reply = OT_FORMAT_CLOCKS;
        else return err(seq, "COMMAND", out, cap);
        *format = reply == OT_FORMAT_COMPUTER ? OT_FORMAT_ADVANCED : reply;
        return ot_match_state(m, reply, seq, out, cap);
    }

    legacy = !strcmp(buf, "@MOVE");
    ai_move = !strcmp(buf, "@AI");
    move = legacy || !strcmp(buf, "@PLAY") || ai_move;
    if (!move && strcmp(buf, "@CTL") && strcmp(buf, "@EDIT")) return err(seq, "COMMAND", out, cap);
    if (count != (move ? 5u : 6u) || !number(fields[2], &revision)) return err(seq, "FORMAT", out, cap);
    if (revision != m->game.revision) return err(seq, "STALE", out, cap);
    if (!strcmp(buf, "@CTL")) {
        for (action = 0; action < sizeof(names) / sizeof(names[0]); ++action)
            if (!strcmp(fields[3], names[action])) break;
        if (action > OT_TOGGLE_EDIT || !number(fields[4], &a) || !number(fields[5], &b))
            return err(seq, "FORMAT", out, cap);
        if (!ot_match_control(m, (OtControl)action, a, b)) return err(seq, "CONTROL", out, cap);
    } else {
        if (!number(fields[3], &a) || !number(fields[4], &b) || a >= 8u || b >= 8u)
            return err(seq, "FORMAT", out, cap);
        if (move) {
            if (m->phase != OT_RUNNING) return err(seq, "NOT_RUNNING", out, cap);
            if (ai_move) {
                if (!ot_match_external_turn(m)) return err(seq, "NOT_AI_TURN", out, cap);
                if (!ot_match_ai_move(m, (uint8_t)a, (uint8_t)b)) return err(seq, "ILLEGAL", out, cap);
            } else {
                if (ot_match_computer_turn(m) || ot_match_external_turn(m))
                    return err(seq, "COMPUTER_TURN", out, cap);
                if (!ot_match_move(m, (uint8_t)a, (uint8_t)b)) return err(seq, "ILLEGAL", out, cap);
            }
        } else {
            if (!number(fields[5], &piece) || piece > OT_WHITE) return err(seq, "FORMAT", out, cap);
            if (!ot_match_edit(m, (uint8_t)a, (uint8_t)b, (uint8_t)piece)) return err(seq, "NOT_EDITING", out, cap);
        }
    }
    if (legacy) *format = OT_FORMAT_BASIC;
    else if (*format < OT_FORMAT_STAGES) *format = OT_FORMAT_ADVANCED;
    return ot_match_state(m, *format, seq, out, cap);
}
