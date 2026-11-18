#include "ot_protocol.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
static void frame(const char *s,char *out){sprintf(out,"%s,%04X\n",s,(unsigned)ot_crc(s,strlen(s)));}
int main(void){OtGame g;char in[160],out[160];unsigned i,n=0;ot_init(&g);
assert(ot_crc("123456789",9)==0x29B1);for(i=0;i<64;i++)n+=ot_legal(&g,i/8,i%8,1);assert(n==4);assert(ot_legal(&g,2,3,1));
frame("@GET,1",in);assert(ot_command(&g,in,out,sizeof(out))>0);assert(strstr(out,"@STATE,1,1,1,0,2,2,"));
frame("@MOVE,2,1,2,3",in);assert(ot_command(&g,in,out,sizeof(out))>0);assert(g.black==4&&g.white==1&&g.turn==2&&g.revision==2);assert(g.board[27]==1);
assert(ot_command(&g,in,out,sizeof(out))>0);assert(strstr(out,"STALE"));
frame("@MOVE,3,2,0,0",in);ot_command(&g,in,out,sizeof(out));assert(strstr(out,"ILLEGAL"));assert(g.revision==2);
ot_command(&g,"@GET,1,0000",out,sizeof(out));assert(strstr(out,"CRC"));
memset(&g,0,sizeof(g));g.turn=1;g.board[0]=1;g.board[1]=2;assert(ot_move(&g,0,2));assert(g.status==OT_OVER&&g.turn==0);
/* Multi-direction flip around D4. */
memset(&g,0,sizeof(g));g.turn=1;g.board[19]=2;g.board[11]=1;g.board[28]=2;g.board[29]=1;g.board[36]=2;g.board[45]=1;assert(ot_move(&g,3,3));assert(g.board[19]==1&&g.board[28]==1&&g.board[36]==1);
puts("PASS: CRC, initial board, legal move, flipping, stale, illegal, end, multi-direction");return 0;}
