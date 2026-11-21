; --- Scripts\SelectTarget.asm ---
use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push ebx
push ecx
push edx
push esi
push edi

mov esi, {TARGET_ID}
test esi, esi
je ExitSelect

mov eax, [0x00E39F5C]
test eax, eax
je ExitSelect

mov ecx, [eax+0x1C]
test ecx, ecx
je ExitSelect

mov ecx, [ecx+0x2C]
test ecx, ecx
je ExitSelect

push esi
call 0x007F45B0

ExitSelect:
pop edi
pop esi
pop edx
pop ecx
pop ebx
mov esp, ebp
pop ebp
ret 4