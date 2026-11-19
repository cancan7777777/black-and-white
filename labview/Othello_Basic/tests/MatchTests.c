#include "ot_match.h"
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
    check(ot_match_move(match, 0u, 2u), "forced terminal move must be legal");
}

static uint32_t random_seed=1u;

static void play_seed(OtMatch *match, uint32_t seed)
{
    uint8_t choices[64];
    while (match->phase == OT_RUNNING) {
        unsigned i, count=0;
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
    for (i=0; i<128u; ++i) {
        ++random_seed;
        ot_match_init(&match);
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
    printf("Legal round scores reaching 188:");
    for (i=OT_WIN_SCORE; i; i=previous[i]) {
        play_seed(&match,used[i]);
        printf(" %u",(unsigned)match.game.black);
        if (previous[i]) check(ot_match_control(&match,OT_START,0u,0u),"continue challenge");
    }
    printf("\n");
    check(match.total==OT_WIN_SCORE && match.result==OT_RESULT_WIN,"legal rounds produce WIN");
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
    check(ot_match_control(&m,OT_SET_MODE,OT_TIMED,1u),"one-second timer");
    ot_match_tick(&m,UINT32_MAX-500u);
    check(!ot_match_tick(&m,498u) && m.remaining_ms==1u,"timer survives tick wrap and leaves one ms");
    check(ot_match_tick(&m,499u) && m.total==2u && !ot_match_move(&m,2u,3u),"exact deadline rejects a late move");
    prove_natural_win();
    puts("PASS native match boundaries");
    return 0;
}
