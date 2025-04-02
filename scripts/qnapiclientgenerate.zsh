#!/bin/zsh

qnapiclientgenerate() {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/WebUI.React
    npm run gen-api-client
    cd $curDir
}