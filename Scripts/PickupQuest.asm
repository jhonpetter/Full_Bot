; --- Scripts\PickupQuest.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push esi

push 0x16
mov ecx, 0x00E12D64
call 0x0098E9B0
mov esi, eax
test esi, esi
je ExitFunc

mov ecx, [0x00E39F5C]
test ecx, ecx
je FreeBuffer

mov eax, 0x00000025
mov [esi], ax
mov dword [esi+0x0A], {QUEST_ID}

push esi
push 0x000000BF
mov dword [esi+0x02], 0x00000007
mov dword [esi+0x06], 0x0000000C
mov dword [esi+0x0E], 0x00000000
mov dword [esi+0x12], 0x00000000

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
mov esp, ebp
pop ebp
ret 0x0C