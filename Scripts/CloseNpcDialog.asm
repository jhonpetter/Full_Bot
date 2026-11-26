use32
org {ALLOC_ADDR}

pushad
mov ecx, {WINDOW_PTR}
test ecx, ecx
je done

mov eax, [ecx]
test eax, eax
je done

call after_str
db "Btn_Close", 0

after_str:
call dword [eax+{DIALOG_FN_OFF}]

done:
popad
ret