#!/bin/zsh

qnuistart() {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/WebUI.React
    npm run dev
}