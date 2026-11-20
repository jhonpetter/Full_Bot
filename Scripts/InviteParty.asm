; --- Scripts\InviteParty.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push edi
push ebx
push esi

mov ebx, {TARGET_PLAYER_ID}

push 0x06
mov ecx, 0x00E12D64
call 0x0098E9B0
mov esi, eax
test esi, esi
je ExitFunc

mov ecx, [0x00E39F5C]
test ecx, ecx
je FreeBuffer

mov eax, 0x0000001B
push esi
mov [esi], ax
push 0x000000AF
mov [esi+0x02], ebx
mov ecx, [ecx+0x20]
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