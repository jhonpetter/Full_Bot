; --- Scripts\SetFlyAlt.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
sub esp, 0x10
push edi
push ebx
push esi

mov eax, [0x00E39F5C]
test eax, eax
je ExitFunc
mov edx, [eax+0x1C]
test edx, edx
je ExitFunc
mov ebx, [edx+0x2C]
test ebx, ebx
je ExitFunc

mov ecx, 0x00E11E44
push 0x00000080
call 0x0098BAA0
mov esi, eax
test esi, esi
je ExitFunc

mov dword [esi], 0x0000014A
mov dword [esi+0x0C], 0x00000001
mov dword [esi+0x10], {FLY_ALT}

mov eax, 0x0000006D
mov ecx, ebx
mov edi, ebx

push esi
call 0x007CE470

ExitFunc:
pop esi
pop ebx
pop edi
mov esp, ebp
pop ebp
ret