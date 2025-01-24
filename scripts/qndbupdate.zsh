#!/bin/zsh

qndbupdate() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/Cex/Cex.Infrastructure
    dotnet ef database update
    cd $curDir
}