#!/bin/zsh

qndbupdate() {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/Cex/Cex.Infrastructure
    dotnet ef database update
    cd $curDir
}