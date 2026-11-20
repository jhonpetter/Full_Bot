; --- Scripts\AutoAttack.asm ---
use32
org {ALLOC_ADDR}

pushad

mov ecx, [0x00E39F5C]
test ecx, ecx
je ExitFunc
mov ecx, [ecx+0x1C]
test ecx, ecx
je ExitFunc
mov ecx, [ecx+0x2C]
test ecx, ecx
je ExitFunc
mov edi, ecx

push 0x00000000
push 0x00000000
mov ebx, [edi+0x0000059C]
push ebx
call 0x007EEB20

ExitFunc:
popad
ret