﻿var editormdView;
$(document).ready(function () {
    editormdView = editormd.markdownToHTML("html_content", {
        tocm: true,    // Using [TOCM]
        taskList: true,
        tex: true,  // 默认不解析
        flowChart: true,  // 默认不解析
        sequenceDiagram: true,  // 默认不解析
    });
});