# Main.vi 错误输入和输出核查

- 用户询问两个相似的灰色错误控件是否要删除。沿用上位机 [#12](https://github.com/cancan7777777/black-and-white/issues/12)，核查起点 `2d8b1b7`。
- 使用现有 LabVIEW ActiveX 工具按格式 2（前面板控件）及 3（已连接的子 VI 接线端）导出文本。两份都列出「错误输入（无错误）」控件及「错误输出」指示器。它们外观相同，分别用来传入原有错误及显示本次运行的错误。现有框图通过错误状态判断何时退出循环。
- 保留两组及既有接线。绿色勾是默认无错误状态，正常运行无需调整。本次为只读核查，保存导出到忽略的构建目录，保留 VI 和用户文件。[NI 错误簇说明](https://www.ni.com/docs/en-US/bundle/labview/page/using-error-clusters.html)
