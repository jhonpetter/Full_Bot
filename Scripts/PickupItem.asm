; --- Scripts\PickupItem.asm ---
use32
org {ALLOC_ADDR}

pushad

mov ebx, {ITEM_SERVER_ID}

mov ecx, [0x00E39F5C]
test ecx, ecx
je ExitFunc
mov ecx, [ecx+0x1C]
test ecx, ecx
je ExitFunc
mov ecx, [ecx+0x2C]
test ecx, ecx
je ExitFunc
mov edi, ecx

push {IS_MAT}
push ebx
call 0x007EEC70

ExitFunc:
popad
ret