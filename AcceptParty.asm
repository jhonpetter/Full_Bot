use32
org {ALLOC_ADDR}

push ebp
mov ebp, esp
push ebx
push esi

; Lê ID do Líder (+0x10) e Timestamp (+0x14) direto do diálogo
mov eax, [{DIALOG_BASE}]
test eax, eax
je ExitFunc
mov edx, [eax+0x10]
mov ebx, [eax+0x14]

; Aloca buffer de 10 bytes (0x0A)
mov ecx, {PACKET_MGR}
push 0x0A
call {FUNC_ALLOC}
mov esi, eax
test esi, esi
je ExitFunc

; Monta pacote 0x001C
mov word [esi], 0x001C
mov [esi+0x02], edx
mov [esi+0x06], ebx

; Valida rede e envia
mov eax, [{BASE_ADDR}]
test eax, eax
je FreeBuffer
mov ecx, [eax+0x20]
test ecx, ecx
je FreeBuffer

push esi
push 0x000000B3
push 0x000000A9
call {FUNC_SEND}

FreeBuffer:
mov ecx, esi
call {FUNC_FREE}

ExitFunc:
pop esi
pop ebx
mov esp, ebp
pop ebp
ret 4
