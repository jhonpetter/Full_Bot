; --- Scripts\AcceptParty.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push ebx
push esi

mov eax, [0x00E3F0E0]
test eax, eax
je ExitFunc
mov ebx, [eax+0x14]

mov ecx, 0x00E12D64
push 0x0A
call 0x0098E9B0
mov esi, eax
test esi, esi
je ExitFunc

mov word [esi], 0x001C
mov dword [esi+0x02], {LEADER_ID}
mov [esi+0x06], ebx

mov eax, [0x00E39F5C]
test eax, eax
je FreeBuffer
mov ecx, [eax+0x20]
test ecx, ecx
je FreeBuffer

push esi
push 0x000000B3
push 0x000000A9
call 0x00909B50

FreeBuffer:
mov ecx, esi
call 0x0098BDF0

ExitFunc:
pop esi
pop ebx
mov esp, ebp
pop ebp
ret 4