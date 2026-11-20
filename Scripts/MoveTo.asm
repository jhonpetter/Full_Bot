; --- Scripts\MoveTo.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
sub esp, 0x20
push edi
push ebx
push esi

mov eax, [0x00E39F5C]
test eax, eax
je ExitFunc
mov edx, [eax+0x1C]
test edx, edx
je ExitFunc
mov edi, [edx+0x2C]
test edi, edi
je ExitFunc

mov ecx, 0x00E11E44
push 0x00000080
call 0x0098BAA0
mov esi, eax
test esi, esi
je ExitFunc

mov dword [esi+0x0C], 0x00000000
mov dword [esi+0x10], {INT_X}
mov dword [esi+0x14], {INT_Z}
mov dword [esi+0x18], {FLY_MODE}

mov ecx, edi
push esi
call 0x007CE470

ExitFunc:
pop esi
pop ebx
pop edi
mov esp, ebp
pop ebp
ret 4