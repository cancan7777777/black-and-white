#include "ot_protocol.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <limits.h>
uint16_t ot_crc(const char *s,size_t n){uint16_t c=0xffff;size_t i;unsigned b;for(i=0;i<n;i++){c^=(uint16_t)((uint8_t)s[i]<<8);for(b=0;b<8;b++)c=(uint16_t)((c&0x8000)?((uint32_t)c<<1)^0x1021u:((uint32_t)c<<1));}return c;}
static int finish(char *out,size_t cap,int n){int k;if(n<0||(size_t)n>=cap)return -1;k=snprintf(out+n,cap-(size_t)n,",%04X\n",(unsigned)ot_crc(out,(size_t)n));return k==6 && (size_t)n+6<cap ? n+k:-1;}
int ot_state(const OtGame *g,uint32_t seq,char *out,size_t cap){char b[65];unsigned i;for(i=0;i<64;i++)b[i]=(char)('0'+g->board[i]);b[64]=0;return finish(out,cap,snprintf(out,cap,"@STATE,%lu,%lu,%u,%u,%u,%u,%s",(unsigned long)seq,(unsigned long)g->revision,g->turn,g->status,g->black,g->white,b));}
static int err(uint32_t seq,const char *why,char *out,size_t cap){return finish(out,cap,snprintf(out,cap,"@ERROR,%lu,%s",(unsigned long)seq,why));}
static int number(const char *s,uint32_t *v){uint32_t n=0;unsigned d;if(!s||!*s)return 0;while(*s){if(*s<'0'||*s>'9')return 0;d=(unsigned)(*s++-'0');if(n>(UINT32_MAX-d)/10u)return 0;n=n*10u+d;}*v=n;return 1;}
static int hex4(const char *s,uint16_t *v){unsigned i,d;uint16_t n=0;if(strlen(s)!=4)return 0;for(i=0;i<4;i++){char c=s[i];if(c>='0'&&c<='9')d=c-'0';else if(c>='A'&&c<='F')d=c-'A'+10;else if(c>='a'&&c<='f')d=c-'a'+10;else return 0;n=(uint16_t)((n<<4)|d);}*v=n;return 1;}
int ot_command(OtGame *g,const char *line,char *out,size_t cap){
    char buf[OT_LINE_MAX],*last,*fields[6],*p;size_t n=strlen(line);unsigned count=1;uint16_t crc;uint32_t seq=0,rev,r,c;
    if(n>=sizeof(buf))return err(0,"LENGTH",out,cap);
    memcpy(buf,line,n+1);while(n&&(buf[n-1]=='\n'||buf[n-1]=='\r'))buf[--n]=0;
    last=strrchr(buf,',');if(!last||!hex4(last+1,&crc)||ot_crc(buf,(size_t)(last-buf))!=crc)return err(0,"CRC",out,cap);
    *last=0;fields[0]=buf;for(p=buf;*p;p++)if(*p==','){if(count>=6)return err(0,"FORMAT",out,cap);*p=0;fields[count++]=p+1;}
    if(count<2||!number(fields[1],&seq)||!seq)return err(0,"FORMAT",out,cap);
    if(!strcmp(fields[0],"@GET")&&count==2)return ot_state(g,seq,out,cap);
    if(!strcmp(fields[0],"@MOVE")&&count==5){
        if(!number(fields[2],&rev)||!number(fields[3],&r)||!number(fields[4],&c)||r>=8||c>=8)return err(seq,"FORMAT",out,cap);
        if(rev!=g->revision)return err(seq,"STALE",out,cap);
        if(!ot_move(g,(uint8_t)r,(uint8_t)c))return err(seq,"ILLEGAL",out,cap);
        return ot_state(g,seq,out,cap);
    }
    return err(seq,"COMMAND",out,cap);
}
