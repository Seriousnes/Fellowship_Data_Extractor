# ModifyAbilityCostForUI semantics

## Verdict

**Replacement.** The float returned by `ModifyAbilityCostForUI` is the cost the UI displays. The
game's own caller passes the base cost in and writes the return straight back over the stack slot
that held it, with no arithmetic between the two. No `mulsd` or `mulss` has the return as an operand
anywhere in the calling routine.

Confidence: high. Two independent lines of evidence agree.

Read from `fellowship-Win64-Shipping.exe` (177,222,144 bytes) at
`C:\Program Files (x86)\Steam\steamapps\common\Fellowship\fellowship\Binaries\Win64\`.
Image base `0x140000000`. All addresses below are virtual addresses in that image.

## The decisive instructions

Call site `14509CB4D` to `14509CB7D`:

```
14509CB4D  lea r9,[rbp-78h]
14509CB51  mov rcx,r14
14509CB54  lea r8,[rbp-7Ch]
14509CB58  lea rdx,[rbp+80h]
14509CB5F  call 00000001451E53F0h
14509CB64  movss xmm3,[rbp-78h]
14509CB69  mov rdx,r14
14509CB6C  movzx r8d,byte [rbp-7Ch]
14509CB71  mov rcx,r13
14509CB74  call 0000000144FACBC0h
14509CB79  test byte [rbp-6Ch],1
14509CB7D  movss [rbp-78h],xmm0
```

`1451E53F0` is the base cost provider. Its prologue writes `mov byte [r8],0` at `1451E540A` and
`mov dword [r9],0` at `1451E5411`, so `r8` is an out parameter for the cost type byte and `r9` an
out parameter for the cost float. The call site passes it `[rbp-7Ch]` and `[rbp-78h]`.

`[rbp-78h]` is then read into `xmm3` at `14509CB64`, the `BaseCost` argument of
`ModifyAbilityCostForUI`, and the wrapper's return in `xmm0` is stored back into the same
`[rbp-78h]` at `14509CB7D`. Same slot in, same slot out, no multiply.

`[rbp-78h]` is the value that reaches the UI record:

```
14509CDF6  movss xmm0,[rbp-78h]
14509CDFB  movss [rdi+58h],xmm0
```

The only arithmetic applied to it after the override is a conditional divide:

```
14509CCFE  movaps xmm1,xmm0
14509CD01  ucomiss xmm1,xmm6
14509CD04  je short 000000014509CD1Dh
14509CD06  cmp byte [rbp+80h],0
14509CD0D  je short 000000014509CD1Dh
14509CD0F  movss xmm0,[rbp-78h]
14509CD14  divss xmm0,xmm1
14509CD18  movss [rbp-78h],xmm0
```

`xmm6` is zeroed at `14509C8F8` by `xorps xmm6,xmm6`, so `ucomiss xmm1,xmm6` compares the divisor
against zero. The divisor comes from a five way switch on the cost type byte at `[rbp-7Ch]`, each
arm fetching a per resource maximum. The routine divides the already overridden cost; it never
multiplies it.

## Corroboration: the native default is the identity on BaseCost

The generated exec thunk at `144FAE990` pops the three parameters off the Blueprint stack and calls
the implementation through the vtable:

```
144FAEA6A  movss xmm3,[rsp+48h]
144FAEA70  test rax,rax
144FAEA73  movzx r8d,byte [rsp+40h]
144FAEA79  mov rdx,[rsp+50h]
144FAEA7E  setne dil
144FAEA82  add rdi,rax
144FAEA85  mov [rbx+20h],rdi
144FAEA89  mov rax,[rsi]
144FAEA8C  call qword [rax+4B8h]
144FAEA92  mov rbx,[rsp+58h]
144FAEA97  movss [rbp],xmm0
```

So vtable slot `+4B8h` takes its float argument in `xmm3`. Two adjacent identity stubs are in the
`UCRGameplayAbility` vtable region:

```
14509E9F0  movaps xmm0,xmm3
14509E9F3  ret
```

```
14509EA00  movaps xmm0,xmm2
14509EA03  ret
```

Only `14509E9F0` can occupy slot `+4B8h`, because only it returns the `xmm3` argument. `14509EA00`
returns `xmm2`, matching the two neighbouring registrations `ModifyCastingDuration` and
`ModifyChannelingDuration`. Their float is the second argument. The vtable arithmetic agrees:
taking the vtable base as `148AC3708`, slot `+4B8h` is `148AC3BC0` and holds `14509E9F0`, and the
following slots `148AC3BC8` and `148AC3BD0` both hold `14509EA00`. That base is confirmed by slot
`+278h` at `148AC3980`. It holds `141569180`, the function the wrapper calls with a `UFunction*`
and a parameter block, and that function appears in 4453 vtables across the image.

`UCRGameplayAbility::ModifyAbilityCostForUI_Implementation` therefore returns its `BaseCost`
argument unchanged. A multiplier contract would make the neutral native default `1.0f`.

## The anchor chain

**1. The name literal.** Exactly one ANSI occurrence of `ModifyAbilityCostForUI`, at `148AC2DE8`
(file offset `0x8AC17E8`, `.rdata`), null terminated on both sides. Exactly one UTF-16 occurrence,
at `148AC2C50` (file offset `0x8AC1650`, `.rdata`). The chain runs through the UTF-16 form: the ANSI
literal has zero code references and is only in registration data.

**2. What holds the ANSI literal.** Three data pointers, all registration records:

- `148ABFE18`, paired with `144FAC0A0`, a lazy `UFunction` construct thunk whose parameter block is
  at `149E52FF0`.
- `148AC3EC0`, paired with `144FAE990`, the exec thunk quoted above.
- `149E52FC0` in `.data`. Its property array pointer at `149E52FD8` is `148AC2DB0`.

The property array at `148AC2DB0` lists the signature, each record's name pointer first and its
parameter offset in the low half of the qword at `+0x30`:

| Record | Name | Kind | Parms offset |
| --- | --- | --- | --- |
| `148AC2C80` | AbilitySystemComponent | Object | `0x00` |
| `148AC2CC0` | UnderlyingType | byte of the enum below | |
| `148AC2D00` | AbilityCostType | Enum | `0x08` |
| `148AC2D40` | BaseCost | Float | `0x0C` |
| `148AC2D78` | ReturnValue | Float | `0x10` |

The signature is `float ModifyAbilityCostForUI(UAbilitySystemComponent*, EAbilityCostType, float
BaseCost)`.

**3. The FName construction stub.** One `lea` reaches the UTF-16 literal:

```
140F2BAF0  mov r8d,1
140F2BAF6  lea rdx,[rel 148AC2C50h]
140F2BAFD  lea rcx,[rel 14A24F7F0h]
140F2BB04  jmp 0000000141339F50h
```

`rcx` is the FName global at `14A24F7F0`. This stub has no `.pdata` entry, so a scan driven by
`.pdata` function ranges misses it; the linear displacement sweep over `.text` finds it.

**4. The global's sole reader.** `14A24F7F0` has exactly two references in the image: the `lea` at
`140F2BAFD` that initialises it, and one read at `144FACBDC` inside the function
`144FACBC0-144FACC6E`:

```
144FACBDC  mov rdx,[rel 14A24F7F0h]
144FACBE3  movaps xmm6,xmm3
144FACBE6  movzx edi,r8b
144FACBEA  mov rbx,rcx
144FACBED  call 0000000141560CF0h
144FACBF2  mov rcx,rax
144FACBF5  mov rbp,rax
144FACBF8  call 0000000141435950h
144FACBFD  mov r10,[rbx]
144FACC00  mov rcx,rbx
144FACC03  mov r9d,[rax+0D4h]
144FACC0A  shr r9d,7
144FACC0E  test r9b,1
144FACC12  jne short 0000000144FACC43h
144FACC14  lea r8,[rsp+20h]
144FACC19  movss [rsp+2Ch],xmm6
144FACC1F  mov rdx,rbp
144FACC22  mov dword [rsp+30h],0
144FACC2A  mov [rsp+20h],rsi
144FACC2F  mov [rsp+28h],dil
144FACC34  call qword [r10+278h]
144FACC3B  movss xmm0,[rsp+30h]
144FACC41  jmp short 0000000144FACC54h
144FACC43  movaps xmm3,xmm6
144FACC46  movzx r8d,dil
144FACC4A  mov rdx,rsi
144FACC4D  call qword [r10+4B8h]
144FACC54  mov rbx,[rsp+60h]
144FACC59  mov rbp,[rsp+68h]
144FACC5E  mov rsi,[rsp+70h]
144FACC63  movaps xmm6,[rsp+40h]
144FACC68  add rsp,50h
144FACC6C  pop rdi
144FACC6D  ret
```

This is the generated wrapper that resolves the function by name. `141560CF0` takes the object in
`rcx` and the FName in `rdx` and returns the `UFunction`. That is kept in `rbp` and passed to the
dispatch below. `141435950` derives a second object from that `UFunction`, and bit 7 of the dword at
its `+0D4h` picks the path: build the parameter block and dispatch through `[r10+278h]` for a
Blueprint override, or call the native implementation directly through `[r10+4B8h]`. Bit 7
corresponds to `CLASS_Native` in the engine's `EClassFlags`, and the branch agrees:
a native owner class takes the direct call and a Blueprint generated one takes the dispatch. The
parameter block it builds at `rsp+20h` matches the reflection offsets exactly: the component at
`+0`, the cost type byte at `+8`, `xmm6` holding `BaseCost` at `+0Ch`, and `ReturnValue` read back
from `+10h`.

Both paths return the float in `xmm0` untouched. The dispatch path loads it at `144FACC3B` and jumps
to `144FACC54`; the native path leaves it in `xmm0` from the call at `144FACC4D` and falls into the
same address. From `144FACC54` to the `ret` at `144FACC6D` the wrapper only restores `rbx`, `rbp`,
`rsi` and `xmm6`, unwinds the frame and pops `rdi`. Nothing scales or replaces `xmm0` between the
two calls and the return.

**5. The wrapper's caller.** `144FACBC0` has exactly one caller in the image, `14509CB74`. It is
in a function whose entry is `14509C760` and whose body runs to `14509CE05` across four `.pdata`
regions (`14509C760-14509C7C4`, `14509C7C4-14509C7CC`, `14509C7CC-14509CA8B`,
`14509CA8B-14509CE05`). That function is itself a virtual, held in seven vtables and at
offset `+4D0h` in the `148AC3708` one, and it fills a UI record at `rdi`: four floats at `+40h`, the cost type byte at `+54h`, the
cost at `+58h`, another float at `+5Ch`, and packed flags at `+64h`. Its call site is the
disassembly quoted at the top.

## Declaring class

`UCRGameplayAbility` in package `/Script/fellowship`. `Z_Construct_UClass` is `144FAB270`, its class
parameter block `148ABFF30`, and the no register function `144FAB990` names the class through the
wide literal at `148ABEAC2` and the package through the wide literal at `1489815F8`.

## Scope of the read

Everything above was parsed statically out of the shipping executable. The game was never launched
and no debugger was attached. Blueprint side callers in the cooked assets were not examined. That
does not weaken the verdict: the single native call site found here is the UI data path itself, it
takes the base cost from the game's own cost provider and it writes the override's return into the
UI record it fills.

## The probe

`C:\Users\Sean\AppData\Local\Temp\claude\G--source-Fellowship-Data-Extractor\7880fb68-6f11-4cd4-af8e-a88ed666256a\scratchpad\CostSemantics\`,
a console project that compiles `src/FellowshipExtractor/Usmap/PeImage.cs` and
`src/FellowshipExtractor/Usmap/ReflectionScanner.cs` by link and references Iced 1.21.0. Commands:
`sections`, `str`, `dataref`, `xref`, `call`, `dis`, `func`, `qwords`, `pdata`, `hunt`, `owner`.

`xref` decodes every `.pdata` function range and matches RIP relative operands and branch targets.
`hunt` sweeps `.text` byte by byte for any displacement that could encode a reference to a target
and back decodes to confirm. That recovers references from code with no unwind data, such
as the FName stub in step 3. `hunt` reports the address of the innermost decode that matches, so an
instruction with a REX prefix is reported one byte high; every address in this document was
re-derived with `dis` or `func`.
