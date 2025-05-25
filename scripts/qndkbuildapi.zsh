qndkbuildapi () {
    local ver=$1
    if [[ ${#ver} -gt 0 ]]; then # Up
        curDir="$(pwd)"
        echo $curDir
        cd $QNEDIR
        
        echo -e "\e[32mdocker build . --no-cache --progress plain --build-arg VERSION=$ver -t qex.api:$ver -f src/WebAPI/Dockerfile\e[0m"  # Green color for selected item
        eval "docker build . --no-cache --progress plain --build-arg VERSION=$ver -t qex.api:$ver -f src/WebAPI/Dockerfile"

        cd $curDir
    fi
}