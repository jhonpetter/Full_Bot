; --- Scripts\FinishQuest.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push esi

push 0x12
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
push esi
mov dword [esi+0x0A], {QUEST_ID}
push 0x000000BB
mov dword [esi+0x02], 0x00000006
mov dword [esi+0x06], 0x00000008
mov dword [esi+0x0E], 0x00000000

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
ret 0x08