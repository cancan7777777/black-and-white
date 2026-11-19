#include "ot_match.h"
#include "app_basic.h"
#include "ot_protocol.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void check(int condition, const char *description)
{
    if (!condition) {
        fprintf(stderr, "FAIL %s\n", description);
        exit(1);
    }
}

static void forced_finish(OtMatch *match, uint8_t side)
{
    memset(match->game.board, 0, sizeof(match->game.board));
    match->game.board[0] = match->game.board[4] = side;
    match->game.board[1] = (uint8_t)(3u - side);
    match->game.black = side == OT_BLACK ? 2u : 1u;
    match->game.white = side == OT_WHITE ? 2u : 1u;
    match->game.turn = side;
    if (ot_match_computer_turn(match)) {
        check(ot_match_tick(match, match->clock_ms + OT_COMPUTER_DELAY_MS), "computer finishes a forced white turn");
    } else {
        check(ot_match_move(match, 0u, 2u), "forced terminal move must be legal");
    }
}

static uint32_t random_seed=1u;

static void play_seed(OtMatch *match, uint32_t seed)
{
    uint8_t choices[64];
    (void)ot_match_tick(match,match->clock_ms);
    while (match->phase == OT_RUNNING) {
        unsigned i, count=0;
        if (ot_match_computer_turn(match)) {
            check(ot_match_tick(match,match->clock_ms+OT_COMPUTER_DELAY_MS),"computer answers the legal black move");
            continue;
        }
        for (i=0; i<64u; ++i)
            if (ot_legal(&match->game,(uint8_t)(i/8u),(uint8_t)(i%8u),match->game.turn))
                choices[count++]=(uint8_t)i;
        check(count!=0u,"running board has a move");
        seed=1664525u*seed+1013904223u;
        i=choices[(seed>>16)%count];
        check(ot_match_move(match,(uint8_t)(i/8u),(uint8_t)(i%8u)),"generated game uses only legal moves");
    }
}

static void prove_natural_win(void)
{
    OtMatch match;
    uint32_t seed_for_score[65]={0}, used[OT_WIN_SCORE+1u]={0};
    unsigned i, score, previous[OT_WIN_SCORE+1u]={0};
    for (i=0; i<512u; ++i) {
        ++random_seed;
        ot_match_init(&match);
        check(ot_match_control(&match,OT_SET_MODE,OT_CHALLENGE,180u),"select human versus computer");
        play_seed(&match,random_seed);
        if (match.game.black>=match.game.white && !seed_for_score[match.game.black])
            seed_for_score[match.game.black]=random_seed;
    }
    for (i=0; i<OT_WIN_SCORE; ++i) {
        if (i && !used[i]) continue;
        for (score=1u; score<=64u && score<=OT_WIN_SCORE-i; ++score) {
            if (seed_for_score[score] && !used[i+score]) {
                used[i+score]=seed_for_score[score];
                previous[i+score]=i;
            }
        }
    }
    check(used[OT_WIN_SCORE]!=0u,"188 is reachable with legal non-losing games");
    ot_match_init(&match);
    check(ot_match_control(&match,OT_SET_MODE,OT_CHALLENGE,180u),"natural proof selects challenge");
    printf("Legal round scores against the computer reaching 188:");
    for (i=OT_WIN_SCORE; i; i=previous[i]) {
        play_seed(&match,used[i]);
        printf(" %u",(unsigned)match.game.black);
        if (previous[i]) check(ot_match_control(&match,OT_START,0u,0u),"continue challenge");
    }
    printf("\n");
    check(match.total==OT_WIN_SCORE && match.result==OT_RESULT_WIN,"legal rounds produce WIN");
}

static char latest_frame[OT_LINE_MAX];

static uint8_t always_ready(void *context) { (void)context; return 1u; }

static uint8_t capture_frame(const char *data, size_t length, void *context)
{
    (void)context;
    if (length >= sizeof(latest_frame)) return 0u;
    memcpy(latest_frame, data, length);
    latest_frame[length] = '\0';
    return 1u;
}

static void exercise_hardware_path(void)
{
    const char request[] = "@SYNC2,1";
    char command[32];
    AppBasicSnapshot snapshot;
    AppBasicPorts ports = { always_ready, capture_frame, NULL, NULL };
    AppBasic_Init(&ports);
    AppBasic_Tick(0u);
    snprintf(command,sizeof(command),"%s,%04X\n",request,(unsigned)ot_crc(request,strlen(request)));
    check(AppBasic_ProcessFrame(command) && !strncmp(latest_frame,"@GAME2,1,",9u),"host can negotiate the computer opponent");
    AppBasic_HandleKey('D');
    AppBasic_HandleKey('5');
    AppBasic_Tick(10u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.turn==OT_WHITE && !strncmp(latest_frame,"@GAME,0,3,2,",12u),"keypad black move is published first");
    AppBasic_HandleKey('5');
    AppBasic_HandleKey('B');
    AppBasic_Tick(1000u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.revision==4u && snapshot.game.turn==OT_WHITE,"pause holds the computer turn; keypad cannot play white");
    AppBasic_HandleKey('B');
    AppBasic_Tick(1240u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.revision==6u && snapshot.game.turn==OT_BLACK && snapshot.game.black==3u && snapshot.game.white==3u &&
        !strncmp(latest_frame,"@GAME,0,6,1,",12u),"after resume, computer plays white and publishes the board without a host move");
}

int main(void)
{
    OtMatch m;
    ot_match_init(&m);
    check(ot_match_control(&m,OT_SET_MODE,OT_CHALLENGE,180u),"challenge configured");
    m.total=OT_WIN_SCORE-4u;
    forced_finish(&m,OT_BLACK);
    check(m.total==188u && m.result==OT_RESULT_WIN,"exactly 188 wins");
    check(ot_match_control(&m,OT_START,0u,0u)&&m.total==0,"restart won session with zero score");
    m.total=OT_WIN_SCORE-3u;
    forced_finish(&m,OT_BLACK);
    check(m.total==189u && m.result==OT_RESULT_BOARD,"greater than 188 does not win");
    check(ot_match_control(&m,OT_START,0u,0u),"next challenge round");
    forced_finish(&m,OT_WHITE);
    check(m.losses==1 && m.result==OT_RESULT_BOARD,"first loss remains playable");
    check(ot_match_control(&m,OT_START,0u,0u),"start after first loss");
    forced_finish(&m,OT_WHITE);
    check(m.losses==2 && m.result==OT_RESULT_OVER,"second consecutive loss stops challenge");
    ot_match_init(&m);
    check(ot_match_control(&m,OT_SET_MODE,OT_TIMED,1u),"one-second timer");
    ot_match_tick(&m,UINT32_MAX-500u);
    check(!ot_match_tick(&m,498u) && m.remaining_ms==1u,"timer survives tick wrap and leaves one ms");
    check(ot_match_tick(&m,499u) && m.total==2u && !ot_match_move(&m,2u,3u),"exact deadline rejects a late move");
    exercise_hardware_path();
    prove_natural_win();
    puts("PASS native match boundaries");
    return 0;
}
