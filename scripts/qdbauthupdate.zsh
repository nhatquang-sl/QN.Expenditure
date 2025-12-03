#!/bin/zsh

qdbauthupdate() {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/Auth/Auth.Infrastructure
    dotnet ef database update
    cd $curDir
}