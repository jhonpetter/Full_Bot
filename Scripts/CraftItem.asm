use32
org {ALLOC_ADDR}

pushad
mov eax, {ITEM_AMOUNT}
push eax
mov eax, {ITEM_ID}
push eax
push 0
mov eax, 00644C50h
call eax
popad
ret