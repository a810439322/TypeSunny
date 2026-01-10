import re

with open('MainWindow.xaml.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

depth = 0
method_stack = []
for i, line in enumerate(lines, 1):
    # 检测方法定义
    if re.search(r'(private|public|protected|internal)\s+(static\s+)?(void|bool|int|string|[A-Z]\w*)\s+\w+\s*\(', line):
        method_match = re.search(r'\s+(\w+)\s*\(', line)
        if method_match and line.strip().endswith('{'):
            method_stack.append((i, method_match.group(1), depth))
    
    before = depth
    depth += line.count('{') - line.count('}')
    
    # 检测方法结束
    if depth < before and method_stack:
        last_method = method_stack[-1]
        if depth == last_method[2]:
            method_stack.pop()

print(f"Final depth: {depth}")
print(f"\nUnclosed methods/blocks:")
for line_num, method_name, d in method_stack:
    print(f"  Line {line_num}: {method_name} (depth {d})")
