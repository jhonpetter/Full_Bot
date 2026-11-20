; --- Scripts\CastSkill.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
sub esp, 0x08
push edi
push ebx
push esi

mov eax, {SKILL_ID}
mov [ebp-0x04], eax

mov eax, [0x00E39F5C]
test eax, eax
je ExitFunc
mov ecx, [eax+0x1C]
test ecx, ecx
je ExitFunc
mov ebx, [ecx+0x2C]
test ebx, ebx
je ExitFunc

mov edi, [ebx+0x0000059C]
mov [ebp-0x08], edi

mov ecx, 0x00E12D64
push 0x0C
call 0x0098E9B0
mov esi, eax
test esi, esi
je ExitFunc

mov word [esi], 0x0029
mov eax, [ebp-0x04]
mov [esi+0x02], eax
mov byte [esi+0x06], 0x01
mov byte [esi+0x07], 0x01
mov eax, [ebp-0x08]
mov [esi+0x08], eax

mov ecx, [0x00E39F5C]
test ecx, ecx
je FreeBuffer
mov ecx, [ecx+0x20]
test ecx, ecx
je FreeBuffer

push esi
push 0x000000B5
push 0x000000A9
call 0x00909B50

mov ecx, esi
call 0x0098BDF0
jmp ExitFunc

FreeBuffer:
mov ecx, esi
call 0x0098BDF0

ExitFunc:
pop esi
pop ebx
pop edi
mov esp, ebp
pop ebp
ret 4