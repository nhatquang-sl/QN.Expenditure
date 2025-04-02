#!/bin/zsh

qnapistart() {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/WebAPI
    dotnet watch run
}