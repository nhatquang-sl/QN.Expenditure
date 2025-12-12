qndkpushapi () {
    local ver=$1
    if [[ ${#ver} -gt 0 ]]; then # Up
        curDir="$(pwd)"
        echo $curDir
        cd $QNEDIR
        
        echo -e "\e[32mqndkbuildapi $ver\e[0m"  # Green color for selected item
        eval "qndkbuildapi $ver"

        echo -e "\e[32mdocker tag qex.api:$ver nhatquang/qex.api:$ver\e[0m"  # Green color for selected item
        eval "docker tag qex.api:$ver nhatquang/qex.api:$ver"

        echo -e "\e[32mdocker push nhatquang/qex.api:$ver\e[0m"  # Green color for selected item
        eval "docker push nhatquang/qex.api:$ver"

        cd $curDir
    fi
}