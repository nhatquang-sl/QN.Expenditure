qncpup () {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR
    
    cmd="docker compose -f credentials/qex/docker-compose.yml -f credentials/qex/docker-compose.override.yml up --build -d"
    echo -e "\e[32m$cmd\e[0m"  # Green color for selected item
    eval $cmd

    cd $curDir
}