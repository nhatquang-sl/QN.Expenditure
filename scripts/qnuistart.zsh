#!/bin/zsh

qnuistart() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/WebUI.React
    npm run dev
}