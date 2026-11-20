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
mov ecx, {PACKET_MGR}
shl esi, 0x02
lea eax, [esi+0x26]
push eax
mov [ebp-0x24], eax
call {FUNC_ALLOCPACKET}
mov edi, eax
test edi, edi
je ExitBuy

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
mov dword [edx+0x04], {SHOP_SLOT}
mov dword [edx+0x08], {AMOUNT}

mov eax, [{BASE_ADDR}]
test eax, eax
je FreeBuffer

mov ecx, [eax+{OFF_NETWORKOFFSET}]
mov eax, [ebp-0x24]
push edi
add eax, 0x000000A9
push eax
push 0x000000A9
call {FUNC_SENDPACKET}

FreeBuffer:
mov ecx, edi
call {FUNC_FREEPACKET}

ExitBuy:
pop edi
pop esi
pop ebx
mov esp, ebp
pop ebp
ret