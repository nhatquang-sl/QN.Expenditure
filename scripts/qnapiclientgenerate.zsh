#!/bin/zsh

qnapiclientgenerate() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/WebUI.React
    npm run gen-api-client
    cd $curDir
}