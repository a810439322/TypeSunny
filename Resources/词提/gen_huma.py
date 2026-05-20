import sys
sys.stdout.reconfigure(encoding='utf-8')

# 填文件路径，推荐自己拷出来把前面无关配置删掉，文件需要  tigress.dict.yaml  tigress_ci.dict.yaml  tigress_simp_ci.dict.yaml
dir = r"E:\nas同步\项目代码\SynologyDrive\TypeSunny\Resources\词提"

# key: (word, code_length) -> (freq, code, is_simp)
# 同一个词在不同码长槽位独立存在，互不干扰
# 同一槽位内：simp 优先，同优先级取频率高的
slot_map = {}

def process_yaml(path, is_simp):
    with open(path, encoding='utf-8-sig') as f:
        for line in f:
            parts = line.rstrip('\n').split('\t')
            if len(parts) < 3:
                continue
            word, freq_s, code = parts[0], parts[1], parts[2]
            if not word or not code:
                continue
            try:
                freq = int(freq_s)
            except ValueError:
                continue
            key = (word, len(code))
            existing = slot_map.get(key)
            if existing is None:
                slot_map[key] = (freq, code, is_simp)
            elif is_simp and not existing[2]:
                slot_map[key] = (freq, code, is_simp)
            elif is_simp == existing[2] and freq > existing[0]:
                slot_map[key] = (freq, code, is_simp)

process_yaml(f"{dir}\\tigress.dict.yaml",         False)
process_yaml(f"{dir}\\tigress_ci.dict.yaml",      False)
process_yaml(f"{dir}\\tigress_simp_ci.dict.yaml", True)

# 按频率降序
sorted_slots = sorted(slot_map.items(), key=lambda x: x[1][0], reverse=True)

# 同编码按出现顺序编号，第一个不满4码加_，后续只加数字
code_count = {}
entries = []
for (word, _), (freq, base_code, is_simp) in sorted_slots:
    n = code_count.get(base_code, 0)
    code_count[base_code] = n + 1
    if n == 0:
        final_code = base_code + '_' if len(base_code) < 4 else base_code
    else:
        final_code = base_code + str(n + 1)
    entries.append((word, final_code))

# 读标点
huma = f"{dir}\\虎码.txt"
punct = []
with open(huma, encoding='utf-8-sig') as f:
    for line in f:
        parts = line.rstrip('\n').split('\t')
        if len(parts) >= 2 and parts[1].startswith(';'):
            punct.append(f"{parts[0]}\t{parts[1]}")

# 后处理：同一个词只保留一条
# 优先选首选（无数字后缀），多个首选或都非首选取最短编码（去掉_后比较）
def is_primary(code):
    return not code[-1].isdigit()

def base_len(code):
    return len(code.rstrip('_'))

word_best = {}  # word -> (code, is_prim, base_len)
for word, code in entries:
    prim = is_primary(code)
    blen = base_len(code)
    if word not in word_best:
        word_best[word] = (code, prim, blen)
    else:
        cur_code, cur_prim, cur_blen = word_best[word]
        # 首选优先；同首选状态取最短
        if (prim and not cur_prim) or (prim == cur_prim and blen < cur_blen):
            word_best[word] = (code, prim, blen)

# 按原排序顺序输出（保持频率降序），每个词只输出一次
seen_words = set()
final_entries = []
for word, code in entries:
    if word not in seen_words and word_best[word][0] == code:
        seen_words.add(word)
        final_entries.append((word, code))

with open(huma, 'w', encoding='utf-8') as f:
    for p in punct:
        f.write(p + '\n')
    for word, code in final_entries:
        f.write(f"{word}\t{code}\n")

print(f"Total: {len(punct) + len(entries)}")

# 验证
with open(huma, encoding='utf-8') as f:
    lines = f.readlines()
for l in lines:
    s = l.rstrip()
    if 'berl' in s or 'ber_' in s or 'jjjr' in s:
        print(s)
