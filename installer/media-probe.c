// SPDX-License-Identifier: GPL-3.0-only
#define WIN32_LEAN_AND_MEAN
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#include <windows.h>
#include <stdio.h>
#include <wchar.h>
#include <string.h>

/* Probe only a user/PATH-selected pair, never a shell. Bound execution time and
   output; a broken tool must not hang setup. All child processes die with job. */
static int version(const wchar_t *file, const char *prefix, char *value) {
    SECURITY_ATTRIBUTES sa = {sizeof(sa), NULL, TRUE};
    HANDLE rd=NULL, wr=NULL, job=NULL, input=INVALID_HANDLE_VALUE; PROCESS_INFORMATION pi={0};
    STARTUPINFOW si={0}; wchar_t command[32768];
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits={0};
    DWORD exitcode=1, used=0, available=0, got=0, start; int ok=0;
    char output[8192];
    if (!CreatePipe(&rd,&wr,&sa,0)) return 0;
    SetHandleInformation(rd,HANDLE_FLAG_INHERIT,0);
    job=CreateJobObjectW(NULL,NULL);
    if (!job) goto done;
    limits.BasicLimitInformation.LimitFlags=JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
    if (!SetInformationJobObject(job,JobObjectExtendedLimitInformation,&limits,sizeof(limits))) goto done;
    si.cb=sizeof(si); si.dwFlags=STARTF_USESTDHANDLES;
    input=CreateFileW(L"NUL",GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE,&sa,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL);
    if (input==INVALID_HANDLE_VALUE) goto done;
    si.hStdOutput=wr; si.hStdError=wr; si.hStdInput=input;
    if (swprintf(command,32768,L"\"%ls\" -version",file)<0) goto done;
    if (!CreateProcessW(file,command,NULL,NULL,TRUE,CREATE_NO_WINDOW|CREATE_SUSPENDED,NULL,NULL,&si,&pi)) {
        fprintf(stderr,"CreateProcess: %lu\n",GetLastError()); goto done;
    }
    if (!AssignProcessToJobObject(job,pi.hProcess)) {fprintf(stderr,"AssignJob: %lu\n",GetLastError()); TerminateProcess(pi.hProcess,1); goto done;}
    ResumeThread(pi.hThread); CloseHandle(wr); wr=NULL; start=GetTickCount();
    for (;;) {
        if (PeekNamedPipe(rd,NULL,0,NULL,&available,NULL) && available) {
            if (available>sizeof(output)-1-used) goto done;
            if (!ReadFile(rd,output+used,available,&got,NULL)) goto done;
            used+=got;
        } else if (WaitForSingleObject(pi.hProcess,20)==WAIT_OBJECT_0) {
            /* A fast tool may write while WaitForSingleObject is waiting. */
            if (PeekNamedPipe(rd,NULL,0,NULL,&available,NULL) && available) continue;
            break;
        }
        if (GetTickCount()-start>3000) goto done;
    }
    if (!GetExitCodeProcess(pi.hProcess,&exitcode) || exitcode) {fprintf(stderr,"Tool exit: %lu\n",exitcode); goto done;}
    output[used]=0;
    if (strncmp(output,prefix,strlen(prefix))) {fprintf(stderr,"Unexpected version output (%lu bytes)\n",used); goto done;}
    const char *p=output+strlen(prefix); size_t n=strcspn(p," \t\r\n");
    if (!n || n>255) goto done;
    memcpy(value,p,n); value[n]=0; ok=1;
done:
    if (job) CloseHandle(job);
    if (pi.hThread) CloseHandle(pi.hThread);
    if (pi.hProcess) {WaitForSingleObject(pi.hProcess,1000); CloseHandle(pi.hProcess);}
    if (wr) CloseHandle(wr);
    if (rd) CloseHandle(rd);
    if (input!=INVALID_HANDLE_VALUE) CloseHandle(input);
    return ok;
}
int wmain(int argc,wchar_t **argv) {
    wchar_t ffmpeg[32768],ffprobe[32768]; char a[256],b[256]; wchar_t wide[256];
    if (argc!=3) return 2;
    if (swprintf(ffmpeg,32768,L"%ls\\ffmpeg.exe",argv[1])<0 ||
        swprintf(ffprobe,32768,L"%ls\\ffprobe.exe",argv[1])<0) return 2;
    if (!version(ffmpeg,"ffmpeg version ",a) || !version(ffprobe,"ffprobe version ",b)) return 3;
    if (strcmp(a,b)) return 4;
    if (!MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,a,-1,wide,256)) return 3;
    if (!WritePrivateProfileStringW(L"tools",L"version",wide,argv[2])) return 5;
    return 0;
}
