; --- Scripts\SellItem.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
sub esp, 0x40
push ebx
push esi
push edi

mov esi, 0x00000001
mov eax, esi
mov ecx, {PACKET_MGR}
shl eax, 0x04
mov [ebp-0x18], eax
add eax, 0x0E
push eax
mov [ebp-0x1C], eax
call {FUNC_ALLOCPACKET}
mov [ebp-0x10], eax
mov edi, eax
test edi, edi
je ExitSell

mov ecx, 0x00000025
mov word [edi], cx
lea edx, [edi+0x02]

mov dword [edx], 0x00000002
mov eax, [ebp-0x18]
lea ecx, [eax+0x04]
mov dword [edx+0x04], ecx
mov dword [edx+0x08], esi

add edx, 0x0C
mov dword [edx], {ITEM_ID}
mov dword [edx+0x04], {BAG_SLOT}
mov dword [edx+0x08], {AMOUNT}
mov dword [edx+0x0C], {UNIT_PRICE}

mov eax, [{BASE_ADDR}]
test eax, eax
je FreeBuffer

mov esi, [ebp-0x10]
mov ecx, [eax+{OFF_NETWORKOFFSET}]
mov eax, [ebp-0x1C]
push esi
add eax, 0x000000A9
push eax
push 0x000000A9
call {FUNC_SENDPACKET}

FreeBuffer:
mov ecx, [ebp-0x10]
call {FUNC_FREEPACKET}

ExitSell:
pop edi
pop esi
pop ebx
mov esp, ebp
pop ebp
ret