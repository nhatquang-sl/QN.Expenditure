#!/bin/zsh

qnapiclientgenerate() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/WebUI.React
    npx nswag run nswag.nswag
    cd $curDir
}