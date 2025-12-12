#!/bin/zsh

qndbremove() {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/Cex/Cex.Infrastructure
    dotnet ef migrations remove -f
    cd $curDir
}