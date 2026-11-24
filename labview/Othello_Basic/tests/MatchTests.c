#include "ot_match.h"
#include "app_basic.h"
#include "app_led_heart.h"
#include "app_led_display.h"
#include "app_keypad.h"
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
    match->game.board[0] = side;
    match->game.board[1] = (uint8_t)(3u - side);
    match->game.black = match->game.white = 1u;
    match->game.turn = side;
    if (ot_match_computer_turn(match)) {
        check(ot_match_tick(match, match->clock_ms + OT_COMPUTER_DELAY_MS), "computer finishes a forced white turn");
    } else {
        check(ot_match_move(match, 0u, 2u), "forced terminal move must be legal");
    }
}

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
    static const uint32_t seeds[] = {23u,33u,49u,1343u};
    OtMatch match;
    unsigned stage;
    ot_match_init(&match);
    check(ot_match_control(&match,OT_SET_MODE,OT_CHALLENGE,180u),"natural proof selects challenge");
    printf("Legal stage scores against the computer:");
    for (stage=1u; stage<=OT_CHALLENGE_STAGES; ++stage) {
        unsigned score;
        play_seed(&match,seeds[stage-1u]);
        score=ot_match_score_half(&match);
        printf(" %u%s",score/2u,score%2u?".5":"");
        if (match.result==OT_RESULT_WIN) break;
        check(match.result==OT_RESULT_BOARD && stage<OT_CHALLENGE_STAGES &&
            ot_match_control(&match,OT_START,0u,0u),"continue challenge");
    }
    printf(" -> %lu%s\n",(unsigned long)match.total,match.total_half_point?".5":"");
    check(match.total>=OT_WIN_SCORE && match.result==OT_RESULT_WIN,"legal stages produce WIN within four rounds");
}

static void check_challenge_choices(const char *cells, const unsigned moves[4])
{
    unsigned stage, square;
    for (stage=1u; stage<=4u; ++stage) {
        OtMatch match;
        unsigned chosen=moves[stage-1u];
        ot_match_init(&match);
        check(ot_match_control(&match,OT_SET_MODE,OT_CHALLENGE,180u),"challenge choice fixture starts");
        match.stage=(uint8_t)stage;
        match.game.turn=OT_WHITE;
        match.game.black=match.game.white=0u;
        for (square=0u; square<64u; ++square) {
            uint8_t piece=(uint8_t)(cells[square]-'0');
            match.game.board[square]=piece;
            match.game.black+=(piece==OT_BLACK);
            match.game.white+=(piece==OT_WHITE);
        }
        check(ot_legal(&match.game,(uint8_t)(chosen/8u),(uint8_t)(chosen%8u),OT_WHITE),"expected stage move is legal");
        check(ot_match_tick(&match,0u) && match.game.board[chosen]==OT_WHITE,"challenge stage chooses its expected move");
    }
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

static void send_command(const char *request)
{
    char command[OT_LINE_MAX];
    snprintf(command, sizeof(command), "%s,%04X\n", request, (unsigned)ot_crc(request, strlen(request)));
    check(AppBasic_ProcessFrame(command), "serial command accepted by app");
}

static unsigned reported_mode(void)
{
    const char *field = latest_frame;
    unsigned comma;
    for (comma = 0u; comma < 8u; ++comma) {
        field = strchr(field, ',');
        check(field != NULL, "mode is present in the state frame");
        ++field;
    }
    return (unsigned)strtoul(field, NULL, 10);
}

static void exercise_standby(void)
{
    AppBasicSnapshot snapshot;
    AppBasicPorts ports = { always_ready, capture_frame, NULL, NULL };
    AppBasic_Init(&ports);
    AppBasic_Tick(0u);
    AppBasic_HandleKey(11u);
    AppBasic_HandleKey(16u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.standby && snapshot.game.black == 0u && snapshot.game.white == 0u &&
        snapshot.game.revision == 2u, "power-on waits for S4 without an opening board or a mode change");
    send_command("@SYNC7,1");
    check(!strncmp(latest_frame,"@GAME7,1,2,0,1,0,0,",19u) &&
        strstr(latest_frame,",180,0,0,0,0,0,1,")!=NULL,"SYNC7 reports a paused display in standby");
    send_command("@CTL,2,2,START,0,0");
    check(strstr(latest_frame,",STANDBY,")!=NULL,"the host cannot start without the hardware switch");
    send_command("@PLAY,3,2,2,3");
    check(strstr(latest_frame,",STANDBY,")!=NULL,"a serial move cannot leave standby");
    AppBasic_HandleKey(app_keypad_button(3u));
    AppBasic_HandleKey(app_keypad_button(14u));
    AppBasic_GetSnapshot(&snapshot);
    check(!snapshot.standby && snapshot.game.black == 2u && snapshot.game.white == 2u &&
        snapshot.cursor_row == 1u && snapshot.cursor_col == 3u,"S4 opens the game and S10 moves up");
    AppBasic_HandleKey(app_keypad_button(11u));
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.cursor_row == 1u && snapshot.cursor_col == 2u,"S15 moves left");
    AppBasic_HandleKey(app_keypad_button(6u));
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.cursor_row == 2u && snapshot.cursor_col == 2u,"S12 moves down");
    AppBasic_HandleKey(app_keypad_button(9u));
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.cursor_row == 2u && snapshot.cursor_col == 3u,"S7 moves right");
    AppBasic_HandleKey(app_keypad_button(10u));
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.board[19] == OT_BLACK && snapshot.game.turn == OT_WHITE,"S11 confirms D3 and changes turn");
    AppBasic_HandleKey(4u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.standby && snapshot.result == OT_RESULT_EXIT && snapshot.game.black == 4u,
        "S4 closes the unfinished round and preserves its final snapshot for the host");
    AppBasic_HandleKey(4u);
    AppBasic_GetSnapshot(&snapshot);
    check(!snapshot.standby && snapshot.result == OT_RESULT_NONE && snapshot.game.black == 2u &&
        snapshot.game.white == 2u && snapshot.game.turn == OT_BLACK,"S4 opens a fresh board after standby");

    check(app_led_board_pixel(OT_EMPTY,1u,OT_BLACK)==0x0E3800u &&
        app_led_board_pixel(OT_EMPTY,1u,OT_WHITE)==0x380038u &&
        app_led_board_pixel(OT_BLACK,0u,OT_WHITE)==0x0C3000u &&
        app_led_board_pixel(OT_WHITE,0u,OT_BLACK)==0x300030u &&
        app_led_board_pixel(OT_EMPTY,0u,OT_WHITE)==0u,
        "empty-square previews blink in the same hue as the committed piece");
}

static void test_welcome_animation(void)
{
    static const unsigned expected_lights[] = {16u,24u,32u,40u,32u,24u};
    unsigned step, row, col;
    for (step=0u; step<APP_LED_WELCOME_STEPS; ++step) {
        unsigned lit=0u;
        for (row=0u; row<8u; ++row) {
            for (col=0u; col<8u; ++col) {
                uint32_t pixel=app_led_welcome_pixel(row,col,step);
                lit+=(pixel!=0u);
                check(pixel==app_led_welcome_pixel(7u-row,col,step) &&
                    pixel==app_led_welcome_pixel(row,7u-col,step),"each welcome frame is symmetric along both axes");
            }
        }
        check(lit==expected_lights[step],"welcome border expands from all four corners and retracts");
    }
    check(app_led_welcome_pixel(3u,3u,0u)==app_led_piece_color(OT_WHITE,28u) &&
        app_led_welcome_pixel(3u,3u,3u)==app_led_piece_color(OT_WHITE,52u),"center diamond pulses as the border expands");
}

static void exercise_hardware_path(void)
{
    AppBasicSnapshot snapshot;
    AppBasicPorts ports = { always_ready, capture_frame, NULL, NULL };
    AppBasic_Init(&ports);
    AppBasic_Tick(0u);
    send_command("@SYNC5,1");
    check(!strncmp(latest_frame,"@GAME5,1,",9u),"host can negotiate external AI");
    AppBasic_HandleKey(4u);
    AppBasic_HandleKey(16u);
    AppBasic_HandleKey(11u);
    AppBasic_Tick(10u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.turn==OT_WHITE && !strncmp(latest_frame,"@GAME5,0,5,2,",13u) &&
        strstr(latest_frame,",180,1,4,0,")!=NULL,"keypad black move publishes the current stage and score");
    AppBasic_HandleKey(11u);
    AppBasic_HandleKey(8u);
    AppBasic_Tick(1000u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.revision==6u && snapshot.game.turn==OT_WHITE,"pause holds the computer turn; keypad cannot play white");
    AppBasic_HandleKey(8u);
    AppBasic_Tick(1240u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.revision==8u && snapshot.game.turn==OT_BLACK && snapshot.game.black==3u && snapshot.game.white==3u &&
        !strncmp(latest_frame,"@GAME5,0,8,1,",13u),"challenge still plays white without a host move");

    AppBasic_HandleKey(16u);
    AppBasic_Tick(1250u);
    check(reported_mode()==OT_CASUAL,"S16 toggles an active challenge off");
    AppBasic_HandleKey(1u);
    AppBasic_Tick(1260u);
    check(reported_mode()==OT_EXTERNAL_AI && strstr(latest_frame,",180,0,4,0,")!=NULL,"S1 starts external AI mode");
    AppBasic_HandleKey(11u);
    AppBasic_Tick(1270u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.turn==OT_WHITE && snapshot.game.black==4u,"board publishes the black move for EN");
    AppBasic_HandleKey(11u);
    AppBasic_Tick(3500u);
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.turn==OT_WHITE && snapshot.game.revision==11u,"keypad and board engine cannot play EN's turn");
    send_command("@PLAY,2,11,2,2");
    check(strstr(latest_frame,",COMPUTER_TURN,")!=NULL,"human serial moves cannot play EN's turn");
    send_command("@AI,3,10,2,2");
    check(strstr(latest_frame,",STALE,")!=NULL,"stale EN result is rejected");
    send_command("@AI,4,11,0,0");
    check(strstr(latest_frame,",ILLEGAL,")!=NULL,"illegal EN result is rejected");
    send_command("@SYNC4,5");
    check(!strncmp(latest_frame,"@GAME4,5,",9u) && reported_mode()==OT_CASUAL,"old hosts can still read the board");
    send_command("@SYNC5,6");
    send_command("@AI,7,11,2,2");
    AppBasic_GetSnapshot(&snapshot);
    check(snapshot.game.turn==OT_BLACK && snapshot.game.black==3u && snapshot.game.white==3u &&
        reported_mode()==OT_EXTERNAL_AI,"firmware confirms the legal EN move");
    send_command("@AI,8,12,0,0");
    check(strstr(latest_frame,",NOT_AI_TURN,")!=NULL,"EN cannot play black's turn");
    AppBasic_HandleKey(1u);
    AppBasic_Tick(3510u);
    check(reported_mode()==OT_CASUAL,"S1 toggles AI off");
    AppBasic_HandleKey(9u);
    AppBasic_Tick(3520u);
    check(reported_mode()==OT_TIMED,"S9 keeps the keypad timer available");
    AppBasic_HandleKey(9u);
    AppBasic_Tick(3530u);
    check(reported_mode()==OT_CASUAL,"S9 toggles timed mode off");
}

static void test_stage_scoring(void)
{
    OtMatch m;
    unsigned stage, accumulated_half=0u;
    ot_match_init(&m);
    check(ot_match_control(&m,OT_SET_MODE,OT_CHALLENGE,180u),"challenge configured");
    for (stage=1u; stage<=OT_CHALLENGE_STAGES; ++stage) {
        check(m.stage==stage,"challenge advances one stage");
        check(ot_match_control(&m,OT_RESTART,0u,0u) && m.stage==stage,"restarting an ongoing stage keeps its coefficient");
        forced_finish(&m,OT_BLACK);
        accumulated_half+=3u*stage;
        check(ot_match_score_half(&m)==3u*stage && m.total*2u+m.total_half_point==accumulated_half,
            "three remaining stones score 1.5, 3, 4.5 and 6 across stages");
        if (stage<OT_CHALLENGE_STAGES) {
            check(m.result==OT_RESULT_BOARD,"a settled stage below the threshold permits the next stage");
            check(ot_match_control(&m,stage==2u?OT_RESTART:OT_START,0u,0u) &&
                m.total*2u+m.total_half_point==accumulated_half,"settled stages advance without duplicate scoring");
        }
    }
    check(m.result==OT_RESULT_OVER && m.losses==0 && m.mode==OT_CHALLENGE &&
        ot_match_active_mode(&m)==OT_CASUAL && ot_match_score_half(&m)==12u,
        "four stages below target exit challenge while retaining the recorded result and score");
    check(ot_match_control(&m,OT_START,0u,0u) && m.mode==OT_CASUAL && m.stage==0u && m.total==0u,
        "the next START after OVER opens an ordinary game");
    check(ot_match_control(&m,OT_SET_MODE,OT_CHALLENGE,180u) && m.stage==1u &&
        m.total==0u && m.total_half_point==0u, "entering challenge again clears points and returns to stage one");
    m.edited=1u;
    forced_finish(&m,OT_BLACK);
    check(ot_match_score_half(&m)==0u && m.total==0u,"an edited stage scores no points");
    check(ot_match_control(&m,OT_START,0u,0u) && m.stage==1u,"an edited stage can be replayed");

    m.total=186u;
    forced_finish(&m,OT_BLACK);
    check(m.total==187u && m.total_half_point==1u && m.result==OT_RESULT_BOARD,"187.5 does not win");
    check(ot_match_control(&m,OT_SET_MODE,OT_CHALLENGE,180u),"reset for exact threshold");
    m.total=186u; m.total_half_point=1u;
    forced_finish(&m,OT_BLACK);
    check(m.total==188u && m.total_half_point==0u && m.result==OT_RESULT_WIN &&
        m.mode==OT_CHALLENGE && ot_match_active_mode(&m)==OT_CASUAL,
        "exactly 188 wins and immediately exits active challenge");
    check(ot_match_control(&m,OT_RESTART,0u,0u) && m.mode==OT_CASUAL && m.total==0u,
        "RESTART after WIN opens an ordinary game with zero score");
    check(ot_match_control(&m,OT_SET_MODE,OT_CHALLENGE,180u),"reset for above threshold");
    m.total=187u;
    forced_finish(&m,OT_BLACK);
    check(m.total==188u && m.total_half_point==1u && m.result==OT_RESULT_WIN,"188.5 also wins");
}

static void test_winning_heart(void)
{
    OtGame game = {0};
    unsigned lit, row, col;
    game.black = 34u;
    game.white = 30u;
    check(app_led_winner(&game, OT_RESULT_NONE) == OT_EMPTY, "running game has no winner");
    game.status = OT_OVER;
    check(app_led_winner(&game, OT_RESULT_BOARD) == OT_BLACK, "board result uses black's color");
    check(app_led_winner(&game, OT_RESULT_WIN) == OT_BLACK, "WIN still has a heart before the marquee");
    check(app_led_winner(&game, OT_RESULT_EXIT) == OT_EMPTY, "early exit skips the heart");
    game.black = 30u;
    check(app_led_winner(&game, OT_RESULT_BOARD) == OT_EMPTY, "draw skips the heart");
    game.white = 34u;
    check(app_led_winner(&game, OT_RESULT_TIME) == OT_WHITE, "timed result uses white's color");
    check(app_led_winner(&game, OT_RESULT_OVER) == OT_WHITE, "OVER still has a heart before the marquee");

    for (lit = 0u; lit <= APP_LED_HEART_PIXELS; ++lit) {
        unsigned count = 0u;
        for (row = 0u; row < 8u; ++row) {
            for (col = 0u; col < 8u; ++col) {
                uint8_t pixel = app_led_heart_pixel(row, col, lit);
                count += pixel;
                if (lit != 0u && app_led_heart_pixel(row, col, lit - 1u))
                    check(pixel, "each revealed LED stays lit");
                if (lit == APP_LED_HEART_PIXELS)
                    check(pixel == app_led_heart_pixel(row, 7u - col, lit), "complete heart is symmetric");
            }
        }
        check(count == lit, "each step lights exactly one new LED");
    }
    check(app_led_heart_visible(0u) == 1u && app_led_heart_visible(44u) == 1u &&
          app_led_heart_visible(45u) == 2u, "45 ms between LEDs");
    check(app_led_heart_visible(1755u) == 40u && app_led_heart_visible(2455u) == 40u,
          "full heart holds after the last LED");
}

int main(void)
{
    OtMatch m;
    ot_match_init(&m);
    check(ot_match_control(&m,OT_SET_MODE,OT_CHALLENGE,180u),"challenge configured");
    forced_finish(&m,OT_WHITE);
    check(m.losses==1 && m.result==OT_RESULT_BOARD,"first loss remains playable");
    check(ot_match_control(&m,OT_START,0u,0u),"start after first loss");
    forced_finish(&m,OT_WHITE);
    check(m.losses==2 && m.result==OT_RESULT_OVER && ot_match_active_mode(&m)==OT_CASUAL,
        "second consecutive loss automatically exits challenge");
    ot_match_init(&m);
    check(ot_match_control(&m,OT_SET_MODE,OT_TIMED,1u),"one-second timer");
    ot_match_tick(&m,UINT32_MAX-500u);
    check(!ot_match_tick(&m,498u) && m.remaining_ms[0]==1u && m.remaining_ms[1]==1000u,"timer survives tick wrap and leaves one ms for black only");
    check(ot_match_tick(&m,499u) && m.total==2u && !ot_match_move(&m,2u,3u),"exact deadline rejects a late move");
    ot_match_init(&m);
    check(ot_match_control(&m,OT_SET_MODE,OT_TIMED,1u),"set one second per side");
    (void)ot_match_tick(&m,0u);
    check(!ot_match_tick(&m,300u) && m.remaining_ms[0]==700u && m.remaining_ms[1]==1000u,"only black spends time before the first move");
    check(ot_match_move(&m,2u,3u) && !ot_match_move(&m,0u,0u),"a legal move changes turns and an illegal move does not");
    check(!ot_match_tick(&m,450u) && m.remaining_ms[0]==700u && m.remaining_ms[1]==850u,"only white spends time after black places a piece");
    check(ot_match_control(&m,OT_PAUSE,0u,0u) && !ot_match_tick(&m,900u) &&
          ot_match_control(&m,OT_RESUME,0u,0u),"pause leaves both clocks frozen");
    check(ot_match_control(&m,OT_TOGGLE_EDIT,0u,0u) && !ot_match_tick(&m,1200u) &&
          ot_match_control(&m,OT_TOGGLE_EDIT,0u,0u),"editing leaves both clocks frozen");
    check(m.remaining_ms[0]==700u && m.remaining_ms[1]==850u,"neither clock moves during pause or edit");
    check(!ot_match_tick(&m,2049u) && m.remaining_ms[1]==1u,"white can still play one ms before its deadline");
    check(ot_match_tick(&m,2100u) && m.result==OT_RESULT_TIME && m.remaining_ms[0]==700u &&
          m.remaining_ms[1]==0u && m.elapsed_ms==1300u && m.total==4u,"only white runs out and an overshoot is not counted");
    check(ot_match_control(&m,OT_START,0u,0u) && m.remaining_ms[0]==1000u && m.remaining_ms[1]==1000u,
          "new round resets both sides");
    test_stage_scoring();
    exercise_standby();
    test_welcome_animation();
    exercise_hardware_path();
    {
        static const unsigned late_choices[] = {15u,7u,63u,7u};
        static const unsigned early_choices[] = {52u,52u,30u,42u};
        check_challenge_choices("1111021021122110212212122121221222122112222222122220120122000000",late_choices);
        check_challenge_choices("0000000000000000000000100002210000011000000010000000000000000000",early_choices);
    }
    prove_natural_win();
    test_winning_heart();
    puts("PASS native match and winning LED heart");
    return 0;
}
