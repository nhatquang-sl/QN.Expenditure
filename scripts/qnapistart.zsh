#!/bin/zsh

qnapistart() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/WebAPI
    dotnet watch run
}