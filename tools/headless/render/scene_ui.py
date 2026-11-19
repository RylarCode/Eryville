#!/usr/bin/env python3
"""Reads the UI canvas out of a Unity scene file (GameScene.unity) into JSON: for every object under the canvas its name, rectangle
(anchors, pivot, position, size), picture colour, text and layout components. The headless town uses it so the render shows the real
scene's authored layout (menus that the game builds in code are added by the game's own code on top).

usage: scene_ui.py <scene.unity> <out.json>
"""
import json, re, sys
import yaml

SCRIPTS = {
    'f4688fdb7df04437aeb418b961361dc5': 'text',
    'fe87c0e1cc204ed48ad3b37840f39efc': 'image',
    '4e29b1a8efbd4b44bb3f3716e73f07ff': 'button',
    '1aa08ab6e0800fa44ae55d278d1423e3': 'scroll',
    '3245ec927659c4140ac4f8d17403cc18': 'fitter',
    '59f8146938fff824cb5fd77236b75775': 'vertical',
    '8a8695521f0d02e499659fee002a26c2': 'grid',
    '31a19414c41e5ae4aae2af33fee712f6': 'mask',
    '0cd44c1031e13a943bb63640046fad76': 'scaler',
}

def load(path):
    text = open(path, encoding='utf8').read()
    docs = {}
    for chunk in re.split(r'(?m)^--- ', text)[1:]:
        head, _, body = chunk.partition('\n')
        m = re.match(r'!u!(\d+) &(\d+)', head)
        if not m or 'stripped' in head:
            continue
        try:
            data = yaml.safe_load(body)
        except yaml.YAMLError:
            continue
        docs[int(m.group(2))] = (int(m.group(1)), data)
    return docs

def v2(d):
    return [float(d['x']), float(d['y'])]

def color(d):
    return [float(d['r']), float(d['g']), float(d['b']), float(d['a'])]

def main(scene, out):
    docs = load(scene)
    # transforms by game object
    rect_of, go_of = {}, {}
    for fid, (cls, body) in docs.items():
        if cls == 224:
            data = body['RectTransform']
            rect_of[data['m_GameObject']['fileID']] = (fid, data)
    objects = {}
    for fid, (cls, body) in docs.items():
        if cls != 1:
            continue
        go = body['GameObject']
        comps = [c['component']['fileID'] for c in go['m_Component']]
        node = {'name': go['m_Name'], 'active': bool(go['m_IsActive']), 'components': {}}
        for cid in comps:
            if cid not in docs:
                continue
            ccls, cbody = docs[cid]
            if ccls != 114:
                continue
            data = cbody['MonoBehaviour']
            kind = SCRIPTS.get(data['m_Script']['guid'])
            if kind:
                node['components'][kind] = data
        objects[fid] = node

    def build(go_id):
        node = objects[go_id]
        out_node = {'name': node['name'], 'active': node['active']}
        tid, rt = rect_of[go_id]
        out_node['anchorMin'] = v2(rt['m_AnchorMin']); out_node['anchorMax'] = v2(rt['m_AnchorMax'])
        out_node['pivot'] = v2(rt['m_Pivot']); out_node['position'] = v2(rt['m_AnchoredPosition']); out_node['size'] = v2(rt['m_SizeDelta'])
        c = node['components']
        if 'image' in c:
            d = c['image']
            sprite = d.get('m_Sprite', {}) or {}
            out_node['image'] = {'color': color(d['m_Color']), 'sprite': bool(sprite.get('fileID')), 'type': d.get('m_Type', 0), 'enabled': bool(d.get('m_Enabled', 1))}
        if 'text' in c:
            d = c['text']
            out_node['text'] = {'text': str(d.get('m_text', '')), 'size': float(d['m_fontSize']), 'color': color(d['m_fontColor']), 'auto': bool(d['m_enableAutoSizing']),
                                'min': float(d['m_fontSizeMin']), 'max': float(d['m_fontSizeMax']), 'h': int(d['m_HorizontalAlignment']), 'v': int(d['m_VerticalAlignment']), 'style': int(d['m_fontStyle'])}
        if 'button' in c: out_node['button'] = True
        if 'scroll' in c: out_node['scroll'] = {'horizontal': bool(c['scroll']['m_Horizontal']), 'vertical': bool(c['scroll']['m_Vertical'])}
        if 'mask' in c: out_node['mask'] = True
        if 'fitter' in c: out_node['fitter'] = {'h': c['fitter']['m_HorizontalFit'], 'v': c['fitter']['m_VerticalFit']}
        if 'vertical' in c:
            d = c['vertical']; p = d['m_Padding']
            out_node['vertical'] = {'padding': [p['m_Left'], p['m_Right'], p['m_Top'], p['m_Bottom']], 'spacing': float(d['m_Spacing']), 'controlW': bool(d['m_ChildControlWidth']), 'controlH': bool(d['m_ChildControlHeight']), 'expandW': bool(d['m_ChildForceExpandWidth']), 'expandH': bool(d['m_ChildForceExpandHeight'])}
        if 'grid' in c:
            d = c['grid']; p = d['m_Padding']
            out_node['grid'] = {'padding': [p['m_Left'], p['m_Right'], p['m_Top'], p['m_Bottom']], 'cell': v2(d['m_CellSize']), 'spacing': v2(d['m_Spacing']), 'constraint': d['m_Constraint'], 'count': d['m_ConstraintCount']}
        out_node['children'] = []
        for ch in rt['m_Children']:
            for gid, (tid2, data) in rect_of.items():
                if tid2 == ch['fileID']:
                    out_node['children'].append(build(gid))
        return out_node

    canvas = None
    for gid, node in objects.items():
        if node['name'] == 'UICanvas':
            canvas = gid
    scaler = None
    for gid, node in objects.items():
        if 'scaler' in node['components']:
            s = node['components']['scaler']
            scaler = {'mode': s['m_UiScaleMode'], 'reference': v2(s['m_ReferenceResolution']), 'match': s['m_MatchWidthOrHeight']}
    tree = build(canvas)
    json.dump({'scaler': scaler, 'canvas': tree}, open(out, 'w'), indent=1)
    print('wrote', out)

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
