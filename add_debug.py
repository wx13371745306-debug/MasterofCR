import sys
import re

files = [
    r'c:\Users\WX133\UnityProject\MasterofCR\Assets\Scripts\Menu\MenuSceneController.cs',
    r'c:\Users\WX133\UnityProject\MasterofCR\Assets\Scripts\Menu\MenuSO.cs',
    r'c:\Users\WX133\UnityProject\MasterofCR\Assets\Scripts\Menu\MenuRecipeCardView.cs',
    r'c:\Users\WX133\UnityProject\MasterofCR\Assets\Scripts\Menu\MenuSelectionUIController.cs',
    r'c:\Users\WX133\UnityProject\MasterofCR\Assets\Scripts\Menu\MenuSelectedItemView.cs',
    r'c:\Users\WX133\UnityProject\MasterofCR\Assets\Scripts\Bond\BondRuntimeBridge.cs'
]

for file in files:
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()

    # match "public class ... {" (handles spaces, newlines leading up to {)
    class_pattern = re.compile(r'(public class [^{]+{)', re.MULTILINE)
    match = class_pattern.search(content)
    if match and 'public bool debugLog' not in content:
        insert_text = '\n    [Header("Debug")]\n    public bool debugLog = false;\n'
        content = content[:match.end()] + insert_text + content[match.end():]
        
    def replacer(m):
        prefix = m.group(1)
        if 'if (debugLog)' in prefix:
            return m.group(0)
        return prefix + 'if (debugLog) Debug.Log('
        
    content = re.sub(r'([ \t]*)Debug\.Log\(', replacer, content)

    with open(file, 'w', encoding='utf-8') as f:
        f.write(content)

print('Updated 6 files with debugLog switch!')
