# 第三方组件与许可

| 文件 | 组件 | 版本 | 许可 | 来源 |
|---|---|---|---|---|
| `three.bundle.js` | [three.js](https://threejs.org) | **r169** | **MIT** | esbuild 打包进单文件（IIFE），随仓分发 |

## MIT 许可全文（three.js）

```
Copyright © 2010-2024 three.js authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## 升级方式

1. 从 npm 取对应版本的 `three` 包；
2. 用 esbuild 打成 IIFE（保持 `window.THREE` 的形态，`index.html` 只 `<script src>` 引入）；
3. 更新本文件的版本号，并**保留** `three.bundle.js` 顶部的 `/*! … */` banner。
