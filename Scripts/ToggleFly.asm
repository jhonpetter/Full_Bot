; --- Scripts\ToggleFly.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
sub esp, 0x10
push edi
push ebx
push esi

mov eax, [0x00E39F5C]
test eax, eax
je ExitFunc
mov edx, [eax+0x1C]
test edx, edx
je ExitFunc
mov ebx, [edx+0x2C]
test ebx, ebx
je ExitFunc

mov ecx, ebx
push 0x00000000
call 0x007C0460

ExitFunc:
pop esi
pop ebx
pop edi
mov esp, ebp
pop ebp
ret 4