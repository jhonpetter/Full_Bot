; --- Scripts\OpenNpcDialog.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push edi
push ebx
push esi

mov esi, {NPC_ID}
test esi, esi
je ExitFunc

mov eax, [0x00E39F5C]
test eax, eax
je ExitFunc

mov ebx, [eax+0x20]
test ebx, ebx
je ExitFunc

mov ecx, [eax+0x1C]
test ecx, ecx
je ExitFunc
mov eax, [ecx+0x2C]
test eax, eax
je ExitFunc

mov [eax+0x0000059C], esi

mov ecx, 0x00E12D64
push 0x00000006
call 0x0098E9B0
mov edi, eax
test edi, edi
je ExitFunc

mov ecx, [0x00E39F5C]
test ecx, ecx
je FreeBuffer
mov eax, 0x00000023

push edi
push 0x000000AF

mov [edi], ax
mov [edi+0x02], esi

mov ecx, [ecx+0x20]
push 0x000000A9

call 0x00909B50

mov ecx, edi
call 0x0098BDF0

mov [ebx+0x000000FC], esi
jmp ExitFunc

FreeBuffer:
mov ecx, edi
call 0x0098BDF0

ExitFunc:
pop esi
pop ebx
pop edi
mov esp, ebp
pop ebp
ret 4