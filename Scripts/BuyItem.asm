; --- Scripts\BuyItem.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
sub esp, 0x40
push ebx
push esi
push edi

mov ebx, 0x00000001
lea esi, [ebx+ebx*2]
mov ecx, 0x00E12D64
shl esi, 0x02
lea eax, [esi+0x26]
push eax
mov [ebp-0x24], eax
call 0x0098E9B0
mov edi, eax
test edi, edi
je ExitFunc

mov word [edi], 0x0025
lea edx, [edi+0x02]
lea ecx, [esi+0x1C]

mov dword [edx], 0x00000001
mov dword [edx+0x04], ecx
mov dword [edx+0x08], 0x00000000
mov dword [edx+0x0C], 0x00000000
mov dword [edx+0x10], 0x00000000
mov dword [edx+0x14], 0x00000000
mov dword [edx+0x18], 0x00000000
mov dword [edx+0x1C], 0x00000000
mov dword [edx+0x20], 0x00000001

add edx, 0x24
mov dword [edx], {ITEM_ID}
mov dword [edx+0x04], {NPC_SLOT}
mov dword [edx+0x08], {ITEM_AMOUNT}

mov eax, [0x00E39F5C]
test eax, eax
je FreeBuffer

mov ecx, [eax+0x20]
mov eax, [ebp-0x24]
push edi
add eax, 0x000000A9
push eax
push 0x000000A9
call 0x00909B50

FreeBuffer:
mov ecx, edi
call 0x0098BDF0

ExitFunc:
pop edi
pop esi
pop ebx
mov esp, ebp
pop ebp
ret