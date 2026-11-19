#include "othello.h"
#include <string.h>
static const int8_t dr[8]={-1,-1,-1,0,0,1,1,1};
static const int8_t dc[8]={-1,0,1,-1,1,-1,0,1};
static int inside(int r,int c){return r>=0 && r<8 && c>=0 && c<8;}
static uint8_t ray(const OtGame *g,int r,int c,uint8_t p,unsigned d){
    int rr=r+dr[d], cc=c+dc[d]; uint8_t n=0, opponent=(uint8_t)(3-p);
    while(inside(rr,cc) && g->board[rr*8+cc]==opponent){n++;rr+=dr[d];cc+=dc[d];}
    return inside(rr,cc) && g->board[rr*8+cc]==p ? n:0;
}
void ot_init(OtGame *g){
    memset(g,0,sizeof(*g));g->board[27]=OT_WHITE;g->board[28]=OT_BLACK;
    g->board[35]=OT_BLACK;g->board[36]=OT_WHITE;g->turn=OT_BLACK;g->black=2;g->white=2;g->revision=1;
}
uint8_t ot_legal(const OtGame *g,uint8_t r,uint8_t c,uint8_t p){
    unsigned d; if(r>=8||c>=8||(p!=1&&p!=2)||g->board[r*8+c]!=0)return 0;
    for(d=0;d<8;d++) {if(ray(g,r,c,p,d))return 1;}return 0;
}
uint8_t ot_has_move(const OtGame *g,uint8_t p){
    unsigned i;for(i=0;i<64;i++)if(ot_legal(g,(uint8_t)(i/8),(uint8_t)(i%8),p))return 1;return 0;
}
uint8_t ot_move(OtGame *g,uint8_t r,uint8_t c){
    unsigned d,i,j;uint8_t lens[8],p=g->turn;
    if(g->status!=OT_PLAY||!ot_legal(g,r,c,p))return 0;
    for(d=0;d<8;d++)lens[d]=ray(g,r,c,p,d);
    g->board[r*8+c]=p;
    for(d=0;d<8;d++)for(j=1;j<=lens[d];j++)g->board[(r+(int)j*dr[d])*8+c+(int)j*dc[d]]=p;
    g->black=0;g->white=0;for(i=0;i<64;i++){g->black+=(g->board[i]==1);g->white+=(g->board[i]==2);}
    g->turn=(uint8_t)(3-p);g->revision++;
    /* Only the side to move is tested: if it has no legal move it loses and the game ends.
       The side that just moved must never be judged here - it already had its turn. */
    if(!ot_has_move(g,g->turn)){g->status=OT_OVER;g->turn=0;}
    return 1;
}
